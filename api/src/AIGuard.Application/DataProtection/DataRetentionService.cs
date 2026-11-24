using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.DataProtection;

/// <param name="EvidenceFrames">Silinen kanıt karesi (dosya ve kayıt).</param>
/// <param name="Sessions">İzleme verisi silinen oturum.</param>
/// <param name="FaceTemplates">Silinen referans yüz vektörü.</param>
/// <param name="ConsentRecords">Silinen rıza kaydı.</param>
/// <param name="AuditLogs">Silinen denetim kaydı.</param>
public sealed record RetentionResult(int EvidenceFrames, int Sessions, int FaceTemplates, int ConsentRecords, int AuditLogs)
{
    public bool Any => EvidenceFrames + Sessions + FaceTemplates + ConsentRecords + AuditLogs > 0;
}

/// <summary>
/// Saklama süresi dolan verilerin silinmesi (A5, FR-35, GAB 3.3.8 veri yaşam döngüsü, K-32). Api'deki arka plan
/// görevi günde bir çağırıyor. Her silme denetim kaydına yazılıyor (FR-36).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Kanıt kareleri: <see cref="EvidenceFrame.DeleteAfter"/> geçince (sınavın saklama süresi).</item>
/// <item>İzleme verisi (olaylar, kareleri, ajan sinyalleri, doğrulama denemeleri): sınavın saklama süresi sonunda.
/// Oturum, skor, cevaplar, notlar ve karar kalıyor; sınav sonucu ve karar silinmiyor.</item>
/// <item>Referans yüz vektörü: adayın son sınavından 30 gün sonra.</item>
/// <item>Rıza ve denetim kayıtları: 1 yıl sonra; hâlâ geçerli bir yüz vektörünün dayandığı rıza kalıyor.</item>
/// </list>
/// </remarks>
public sealed class DataRetentionService(IAppDbContext db, IEvidenceStore store, TimeProvider clock)
{
    public static readonly TimeSpan FaceTemplateRetention = TimeSpan.FromDays(30);

    public static readonly TimeSpan RecordRetention = TimeSpan.FromDays(365);

    // Veritabanı tetikleyicisi yalnızca "1 yıldan eski" denetim kaydının silinmesine izin veriyor (K-32);
    // uygulama ve veritabanı saati arasındaki küçük farka takılmamak için 1 gün pay bırakılıyor.
    private static readonly TimeSpan AuditLogMargin = TimeSpan.FromDays(1);

    private const int BatchSize = 200;

    private static readonly SessionState[] Running = [.. SessionStateMachine.Running];

    public async Task<RetentionResult> RunOnceAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();

        var result = new RetentionResult(
            EvidenceFrames: await DeleteExpiredEvidenceAsync(now, ct),
            Sessions: await PurgeSessionsAsync(now, ct),
            FaceTemplates: await DeleteStaleFaceTemplatesAsync(now, ct),
            ConsentRecords: await DeleteOldConsentRecordsAsync(now, ct),
            AuditLogs: await DeleteOldAuditLogsAsync(now, ct));

        if (result.Any)
        {
            // Oturum ve yüz vektörü silmeleri ayrıca kendi kayıtlarıyla yazıldı; bu satır turun özeti.
            db.AuditLogs.Add(new AuditLog
            {
                Action = "retention.cleanup",
                NewValue = JsonSerializer.Serialize(result),
                OccurredAt = now,
            });
            await db.SaveChangesAsync(ct);
        }

        return result;
    }

    private async Task<int> DeleteExpiredEvidenceAsync(DateTimeOffset now, CancellationToken ct)
    {
        var total = 0;
        while (true)
        {
            var batch = await db.EvidenceFrames.AsNoTracking()
                .Where(f => f.DeleteAfter <= now)
                .OrderBy(f => f.DeleteAfter)
                .Select(f => new { f.Id, f.StorageKey })
                .Take(BatchSize)
                .ToListAsync(ct);
            if (batch.Count == 0)
            {
                return total;
            }

            // Önce dosya, sonra kayıt: kayıt kalırsa sonraki tur siler (bağlantı 404 döner); tersi olsaydı
            // şifreli dosya hiçbir kayda bağlı olmadan diskte kalırdı.
            foreach (var frame in batch)
            {
                await store.DeleteAsync(frame.StorageKey, ct);
            }

            var ids = batch.Select(f => f.Id).ToList();
            db.EvidenceFrames.RemoveRange(await db.EvidenceFrames.Where(f => ids.Contains(f.Id)).ToListAsync(ct));
            total += await db.SaveChangesAsync(ct);
            if (batch.Count < BatchSize)
            {
                return total;
            }
        }
    }

    private async Task<int> PurgeSessionsAsync(DateTimeOffset now, CancellationToken ct)
    {
        // Saklama süresi en az 1 gün; bu ön eleme sorguyu küçültüyor, kesin kontrol aşağıda bellekte.
        var candidates = await db.ExamSessions.AsNoTracking()
            .Where(s => s.MonitoringDataDeletedAt == null && !Running.Contains(s.State))
            .Select(s => new { s.Id, s.State, s.EndedAt, s.CreatedAt, s.Exam.WindowEndAt, s.Exam.RetentionDays })
            .ToListAsync(ct);

        var due = candidates
            .Where(s => RetentionStart(s.State, s.EndedAt, s.CreatedAt, s.WindowEndAt).AddDays(s.RetentionDays) <= now)
            .Select(s => s.Id)
            .ToList();

        foreach (var sessionId in due)
        {
            await PurgeSessionAsync(sessionId, now, ct);
        }

        return due.Count;
    }

    /// <summary>
    /// Saklama süresinin başladığı an: biten oturumda bitiş, hiç başlamayan oturumda sınav penceresinin kapanışı
    /// (o andan sonra oturum artık başlayamıyor).
    /// </summary>
    public static DateTimeOffset RetentionStart(SessionState state, DateTimeOffset? endedAt, DateTimeOffset createdAt, DateTimeOffset windowEndAt) =>
        SessionStateMachine.Closed.Contains(state) ? endedAt ?? createdAt : windowEndAt;

    private async Task PurgeSessionAsync(Guid sessionId, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Aynı anda yanlış alarm işaretleyen ya da olay yazan istekle yarışmamak için (K-18).
        await db.LockExamSessionAsync(sessionId, ct);
        var session = await db.ExamSessions.FirstAsync(s => s.Id == sessionId, ct);
        if (session.MonitoringDataDeletedAt is not null)
        {
            return;
        }

        var frames = await db.EvidenceFrames.AsNoTracking()
            .Where(f => f.Event.ExamSessionId == sessionId)
            .Select(f => f.StorageKey)
            .ToListAsync(ct);
        foreach (var key in frames)
        {
            await store.DeleteAsync(key, ct);
        }

        // Kanıt karesi kayıtları olayla birlikte siliniyor (veritabanında cascade).
        var events = await db.Events.Where(e => e.ExamSessionId == sessionId).ToListAsync(ct);
        var signals = await db.AgentSignals.Where(s => s.AgentPairing.ExamSessionId == sessionId).ToListAsync(ct);
        var verifications = await db.Verifications.Where(v => v.ExamSessionId == sessionId).ToListAsync(ct);
        db.Events.RemoveRange(events);
        db.AgentSignals.RemoveRange(signals);
        db.Verifications.RemoveRange(verifications);

        session.MonitoringDataDeletedAt = now;
        db.AuditLogs.Add(new AuditLog
        {
            Action = "retention.session_purged",
            TargetType = nameof(ExamSession),
            TargetId = sessionId,
            NewValue = JsonSerializer.Serialize(new
            {
                events = events.Count,
                evidenceFrames = frames.Count,
                agentSignals = signals.Count,
                verifications = verifications.Count,
            }),
            OccurredAt = now,
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<int> DeleteStaleFaceTemplatesAsync(DateTimeOffset now, CancellationToken ct)
    {
        var cutoff = now - FaceTemplateRetention;

        // Son sınav: adayın oturumlarındaki en son hareket (bitiş, son istek ya da açılış). Hiç sınava girmemiş
        // adayda süre kayıt anından sayılıyor. Süren sınavın son isteği hep yeni olduğu için vektörü silinmiyor.
        var stale = await db.ReferenceFaceTemplates
            .Where(t => t.CreatedAt <= cutoff)
            .Where(t => !db.ExamSessions.Any(s => s.CandidateId == t.UserId && (s.EndedAt ?? s.LastSeenAt ?? s.CreatedAt) > cutoff))
            .ToListAsync(ct);

        foreach (var template in stale)
        {
            db.ReferenceFaceTemplates.Remove(template);
            db.AuditLogs.Add(new AuditLog
            {
                Action = "retention.face_template_deleted",
                TargetType = nameof(User),
                TargetId = template.UserId,
                NewValue = JsonSerializer.Serialize(new { reason = "last_exam_30_days" }),
                OccurredAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
        return stale.Count;
    }

    private async Task<int> DeleteOldConsentRecordsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var cutoff = now - RecordRetention;

        // Geri alınmış rızada süre geri alma anından, diğerlerinde veriliş anından sayılıyor. Hâlâ duran bir yüz
        // vektörünün dayandığı rıza silinmiyor; vektör silinince sonraki turlarda sırası geliyor.
        var old = await db.ConsentRecords
            .Where(c => (c.WithdrawnAt ?? c.GrantedAt) <= cutoff)
            .Where(c => !db.ReferenceFaceTemplates.Any(t => t.ConsentRecordId == c.Id))
            .ToListAsync(ct);
        db.ConsentRecords.RemoveRange(old);
        await db.SaveChangesAsync(ct);
        return old.Count;
    }

    private Task<int> DeleteOldAuditLogsAsync(DateTimeOffset now, CancellationToken ct) =>
        db.DeleteAuditLogsOlderThanAsync(now - RecordRetention - AuditLogMargin, ct);
}
