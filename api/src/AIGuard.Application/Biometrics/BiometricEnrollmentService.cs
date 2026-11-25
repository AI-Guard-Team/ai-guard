using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Application.ConsentTexts;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Biometrics;

public enum EnrollStatus
{
    Enrolled,

    /// <summary>Ayrı açık rıza verilmedi (FR-04).</summary>
    ConsentRequired,

    /// <summary>Adayın onayladığı metin güncel sürüm değil.</summary>
    ConsentVersionMismatch,

    /// <summary>Dosya boş, çok büyük ya da JPEG/PNG/WebP değil.</summary>
    InvalidImage,

    /// <summary>Görüntü servisi fotoğrafı reddetti; neden <see cref="EnrollResult.FaceIssue"/>'da.</summary>
    NoUsableFace,

    /// <summary>Kullanıcı yok, pasif ya da aday değil.</summary>
    NotCandidate,

    /// <summary>Doğrulaması yapılmış ya da süren bir oturum varken referans değiştirilemez.</summary>
    SessionActive,
}

/// <param name="FaceIssue">Fotoğraf reddedildiyse görüntü servisinin verdiği neden (<see cref="Abstractions.FaceIssue"/>).</param>
public sealed record EnrollResult(EnrollStatus Status, string? FaceIssue = null);

public sealed record WithdrawResult(bool UserFound, int CancelledSessions);

/// <summary>Kayıt fotoğrafı ve biyometrik rıza (A3; FR-04, FR-05).</summary>
/// <summary><c>GET /api/me/biometric</c> yanıtı (UI1).</summary>
public sealed record BiometricStatus(bool Enrolled, DateTimeOffset? EnrolledAt, string? ConsentTextVersion);

public sealed class BiometricEnrollmentService(
    IAppDbContext db,
    IFaceService faces,
    IFieldEncryptor encryptor,
    TimeProvider clock)
{
    // Doğrulaması geçmiş ya da geçmek üzere olan oturumlar. Bu sırada referans değişirse aday
    // sınavın ortasında başka birinin fotoğrafını yükleyip yeniden girişte onunla doğrulanabilir.
    private static readonly SessionState[] StatesLockingReference =
        [SessionState.IdentityVerification, SessionState.InProgress, SessionState.PermissionPending, SessionState.Disconnected];

    private static readonly SessionState[] ClosedStates = [.. SessionStateMachine.Closed];

    /// <summary>
    /// Adayın kayıt fotoğrafı (yüz şablonu) var mı (UI1). Vektör ve fotoğraf dönmüyor; yalnızca durum,
    /// oluşturulma zamanı ve kabul edilen rıza metninin sürümü.
    /// </summary>
    public async Task<BiometricStatus> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var template = await db.ReferenceFaceTemplates.AsNoTracking()
            .Where(t => t.UserId == userId)
            .Select(t => new { t.CreatedAt, t.ConsentRecord.TextVersion })
            .SingleOrDefaultAsync(ct);

        return template is null
            ? new BiometricStatus(false, null, null)
            : new BiometricStatus(true, template.CreatedAt, template.TextVersion);
    }

    /// <summary>Şifreli vektörü sahibine bağlayan bağlam (bkz. <see cref="IFieldEncryptor"/>).</summary>
    public static string TemplateContext(Guid userId) => $"face-template:{userId:N}";

    /// <remarks>
    /// Fotoğraf yalnızca görüntü servisine iletiliyor; veritabanına ya da diske yazılmıyor ve
    /// servis yanıt verdikten sonra dizi sıfırlanıyor. Saklanan tek şey şifreli vektör.
    /// </remarks>
    public async Task<EnrollResult> EnrollAsync(
        Guid userId, bool consent, string? consentTextVersion, byte[] photo, CancellationToken ct = default)
    {
        try
        {
            return await EnrollCoreAsync(userId, consent, consentTextVersion, photo, ct);
        }
        finally
        {
            Array.Clear(photo);
        }
    }

    private async Task<EnrollResult> EnrollCoreAsync(
        Guid userId, bool consent, string? consentTextVersion, byte[] photo, CancellationToken ct)
    {
        if (!consent)
        {
            return new(EnrollStatus.ConsentRequired);
        }

        // Aday güncel metni görmüş olmalı; metin yeni sürümle değiştiyse eski sürümle rıza alınmıyor (K-32).
        if (!await db.IsCurrentVersionAsync(ConsentType.Biometric, consentTextVersion, ct))
        {
            return new(EnrollStatus.ConsentVersionMismatch);
        }

        var textVersion = consentTextVersion!;

        if (!ImageFile.IsAcceptable(photo, ImageFile.MaxPhotoBytes))
        {
            return new(EnrollStatus.InvalidImage);
        }

        var isCandidate = await db.Users.AnyAsync(u => u.Id == userId && u.IsActive && u.Role == UserRole.Candidate, ct);
        if (!isCandidate)
        {
            return new(EnrollStatus.NotCandidate);
        }

        if (await db.ExamSessions.AnyAsync(s => s.CandidateId == userId && StatesLockingReference.Contains(s.State), ct))
        {
            return new(EnrollStatus.SessionActive);
        }

        var enrolled = await faces.EnrollAsync(photo, ct);
        if (enrolled.Rejected)
        {
            return new(EnrollStatus.NoUsableFace, enrolled.Issue);
        }

        // İstemci zaten denetliyor; başka bir IFaceService uygulaması bozuk vektör dönerse de saklanmasın.
        if (!FaceVector.IsValid(enrolled.Vector))
        {
            throw new FaceServiceUnavailableException("Görüntü servisi geçersiz bir yüz vektörü döndü.");
        }

        var now = clock.GetUtcNow();
        var consentRecord = new ConsentRecord
        {
            UserId = userId,
            Type = ConsentType.Biometric,
            TextVersion = textVersion,
            GrantedAt = now,
        };
        db.ConsentRecords.Add(consentRecord);

        var encrypted = encryptor.Encrypt(FaceVector.ToBytes(enrolled.Vector!), TemplateContext(userId));
        var template = await db.ReferenceFaceTemplates.SingleOrDefaultAsync(t => t.UserId == userId, ct);
        if (template is null)
        {
            template = new ReferenceFaceTemplate { UserId = userId, EncryptedVector = encrypted.Ciphertext };
            db.ReferenceFaceTemplates.Add(template);
        }

        // Yeniden yüklemede aynı kayıt güncelleniyor (aday başına tek şablon, K-11). Önceki rıza
        // kaydı geçmiş olarak kalıyor; şablon yeni rızaya bağlanıyor.
        template.EncryptedVector = encrypted.Ciphertext;
        template.KeyVersion = encrypted.KeyVersion;
        template.ConsentRecordId = consentRecord.Id;
        template.CreatedAt = now;

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            Action = "biometric.enroll",
            TargetType = nameof(ReferenceFaceTemplate),
            TargetId = template.Id,
            NewValue = JsonSerializer.Serialize(new { consentTextVersion = textVersion }),
            OccurredAt = now,
        });

        await db.SaveChangesAsync(ct);
        return new(EnrollStatus.Enrolled);
    }

    /// <summary>
    /// Biyometrik rızayı geri alır: rıza kayıtları kapatılır, yüz vektörü silinir ve açık oturumlar
    /// iptal edilir (FR-05). Tekrar çağrılması zararsız.
    /// </summary>
    /// <remarks>
    /// İptal, durum makinesinin izin verdiği oturumlarda yapılıyor (K-24): rıza adımı ve sınav sırası.
    /// Eşleştirme ve doğrulama adımındaki oturumlar için tabloda Cancelled geçişi yok; bunlar olduğu
    /// yerde kalıyor ve vektör silindiği için doğrulamayı geçemiyor (S-23).
    /// </remarks>
    public async Task<WithdrawResult> WithdrawConsentAsync(Guid userId, CancellationToken ct = default)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId, ct))
        {
            return new WithdrawResult(false, 0);
        }

        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var consents = await db.ConsentRecords
            .Where(c => c.UserId == userId && c.Type == ConsentType.Biometric && c.WithdrawnAt == null)
            .ToListAsync(ct);
        foreach (var consent in consents)
        {
            consent.WithdrawnAt = now;
        }

        var template = await db.ReferenceFaceTemplates.SingleOrDefaultAsync(t => t.UserId == userId, ct);
        if (template is not null)
        {
            db.ReferenceFaceTemplates.Remove(template);
        }

        var sessionIds = await db.ExamSessions
            .Where(s => s.CandidateId == userId && !ClosedStates.Contains(s.State))
            .Select(s => s.Id)
            .ToListAsync(ct);

        var cancelled = 0;
        foreach (var sessionId in sessionIds)
        {
            // Aynı anda olay yazan ya da teslim eden istekle yarışmamak için satır kilitleniyor (K-18).
            await db.LockExamSessionAsync(sessionId, ct);
            var session = await db.ExamSessions.FirstAsync(s => s.Id == sessionId, ct);
            if (!SessionStateMachine.CanTransition(session.State, SessionState.Cancelled))
            {
                continue;
            }

            var oldState = session.State;
            session.TransitionTo(SessionState.Cancelled);
            session.EndedAt = now;
            cancelled++;

            db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = userId,
                Action = "session.cancel",
                TargetType = nameof(ExamSession),
                TargetId = sessionId,
                OldValue = JsonSerializer.Serialize(new { state = oldState.ToString() }),
                NewValue = JsonSerializer.Serialize(new { state = nameof(SessionState.Cancelled), reason = "biometric-consent-withdrawn" }),
                OccurredAt = now,
            });
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            Action = "consent.withdraw",
            TargetType = nameof(User),
            TargetId = userId,
            NewValue = JsonSerializer.Serialize(new
            {
                type = nameof(ConsentType.Biometric),
                templateDeleted = template is not null,
                cancelledSessions = cancelled,
            }),
            OccurredAt = now,
        });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new WithdrawResult(true, cancelled);
    }
}
