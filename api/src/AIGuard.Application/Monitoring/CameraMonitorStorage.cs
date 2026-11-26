using System.Security.Cryptography;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Biometrics;
using AIGuard.Application.Events;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Monitoring;

public enum EpisodeWriteStatus
{
    Recorded,

    /// <summary>Oturum kapalı ya da yok; durum silinmeli.</summary>
    SessionClosed,

    /// <summary>Olay servisi reddetti (ör. geçersiz değer); yalnızca günlüğe yazılıyor.</summary>
    Rejected,
}

/// <summary>Kamera izlemenin veritabanı ve kanıt deposu işleri; birim testlerinde sahtesi kullanılıyor.</summary>
public interface ICameraMonitorStorage
{
    /// <summary>Oturumun adayının referans vektörünü çözer; şablon yoksa ya da çözülemezse <c>null</c>.</summary>
    Task<float[]?> LoadReferenceVectorAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Bölümü tek olay olarak yazar; kanıt karesi varsa şifreli saklayıp olaya bağlar.</summary>
    Task<EpisodeWriteStatus> RecordAsync(Guid sessionId, CameraEpisode episode, CancellationToken ct = default);

    /// <summary>Verilenlerden artık sürmeyen (kapanmış ya da silinmiş) oturumlar.</summary>
    Task<IReadOnlyCollection<Guid>> FindEndedAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken ct = default);
}

public sealed class CameraMonitorStorage(
    IAppDbContext db, EventService events, IEvidenceStore evidence, IFieldEncryptor encryptor, TimeProvider clock)
    : ICameraMonitorStorage
{
    public async Task<float[]?> LoadReferenceVectorAsync(Guid sessionId, CancellationToken ct = default)
    {
        var row = await db.ExamSessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new { s.CandidateId, Template = s.Candidate.ReferenceFaceTemplate })
            .SingleOrDefaultAsync(ct);
        if (row?.Template is not { } template)
        {
            return null;
        }

        try
        {
            return FaceVector.FromBytes(encryptor.Decrypt(
                template.EncryptedVector, template.KeyVersion, BiometricEnrollmentService.TemplateContext(row.CandidateId)));
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            // Bozuk ya da başka bir adayın satırından kopyalanmış vektör (K-26).
            return null;
        }
    }

    public async Task<EpisodeWriteStatus> RecordAsync(Guid sessionId, CameraEpisode episode, CancellationToken ct = default)
    {
        // Olay süresinin üst sınırı 1 sa (EventService); PERMISSION_REVOKED'daki gibi sınırda yazılıyor.
        var duration = Math.Clamp(episode.DurationSec, 0, EventService.MaxDurationSec);
        var stored = episode.Evidence is { } image ? await evidence.SaveAsync(image, ct) : null;
        try
        {
            // Oturum bitişinde (SessionService.CompleteAsync) çağıranın işlemine katılıyor.
            var ownsTransaction = db.Database.CurrentTransaction is null;
            await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;

            var result = await events.RecordSystemEventAsync(sessionId,
                new SystemEvent(episode.Type, episode.StartedAt, duration, episode.Confidence, episode.Payload), ct);
            if (result.Status != EventRecordStatus.Recorded)
            {
                await DeleteAsync(stored);
                return result.Status is EventRecordStatus.SessionClosed or EventRecordStatus.SessionNotFound
                    ? EpisodeWriteStatus.SessionClosed
                    : EpisodeWriteStatus.Rejected;
            }

            if (stored is not null)
            {
                var now = clock.GetUtcNow();
                var retentionDays = await db.ExamSessions.Where(s => s.Id == sessionId).Select(s => s.Exam.RetentionDays).SingleAsync(ct);
                db.EvidenceFrames.Add(new EvidenceFrame
                {
                    EventId = result.Event!.Id,
                    StorageKey = stored.StorageKey,
                    KeyVersion = stored.KeyVersion,
                    CreatedAt = now,
                    DeleteAfter = now.AddDays(retentionDays),
                });
                await db.SaveChangesAsync(ct);
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }

            return EpisodeWriteStatus.Recorded;
        }
        catch
        {
            // Kaydı yazılamayan kare dosyası kalmasın (K-26).
            await DeleteAsync(stored);
            throw;
        }
    }

    public async Task<IReadOnlyCollection<Guid>> FindEndedAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken ct = default)
    {
        var running = SessionStateMachine.Running.ToArray();
        var alive = await db.ExamSessions.AsNoTracking()
            .Where(s => sessionIds.Contains(s.Id) && running.Contains(s.State))
            .Select(s => s.Id)
            .ToListAsync(ct);
        return sessionIds.Except(alive).ToList();
    }

    private async Task DeleteAsync(StoredEvidence? stored)
    {
        if (stored is not null)
        {
            await evidence.DeleteAsync(stored.StorageKey, CancellationToken.None);
        }
    }
}
