using System.Security.Cryptography;
using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Application.ConsentTexts;
using AIGuard.Application.Events;
using AIGuard.Application.Sessions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Domain.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AIGuard.Application.Biometrics;

public enum SessionConsentStatus
{
    Accepted,

    /// <summary>Aday rızayı reddetti; oturum iptal edildi, hak kullanılmadı (FR-10).</summary>
    Refused,

    SessionNotFound,

    /// <summary>Oturum rıza adımında değil.</summary>
    InvalidState,

    ConsentVersionMismatch,

    /// <summary>Kayıt fotoğrafı yok; sınav başlatılamaz (FR-04).</summary>
    NoReferencePhoto,
}

public enum VerifyStatus
{
    Passed,

    /// <summary>Canlılık testi geçmedi ya da karede yüz yok: "Canlı yüz algılanamadı" (UC-12, 4a).</summary>
    LivenessFailed,

    /// <summary>Yüz kayıt fotoğrafıyla eşleşmedi: "Kimlik doğrulanamadı" (UC-12, 4b).</summary>
    IdentityMismatch,

    SessionNotFound,

    /// <summary>Oturum doğrulama ya da yeniden giriş adımında değil, ya da sınav süresi doldu.</summary>
    InvalidState,

    /// <summary>Kare sayısı 3–5 değil, kare çok büyük ya da desteklenen bir görüntü değil.</summary>
    InvalidFrames,

    NoReferencePhoto,

    /// <summary>Bir önceki denemenin üzerinden bekleme süresi geçmedi.</summary>
    TooSoon,
}

public sealed record VerifyResult(VerifyStatus Status, VerificationKind? Kind = null);

/// <summary>Oturum rızası ve sınav başı / yeniden giriş doğrulaması (A3; FR-04, FR-10, UC-12).</summary>
/// <remarks>
/// Durum geçişleri E4'ün durum makinesinden (K-24): rızada <c>TransitionTo</c>, başarılı doğrulamada
/// <see cref="SessionService.BeginExamAsync"/> ya da <see cref="SessionService.ResumeAfterReconnectAsync"/>.
/// Süre ve oturum hakkı orada başlatılıyor.
/// </remarks>
public sealed class SessionVerificationService(
    IAppDbContext db,
    IFaceService faces,
    IFieldEncryptor encryptor,
    IEvidenceStore evidence,
    EventService events,
    SessionService sessions,
    IOptions<BiometricOptions> options,
    TimeProvider clock)
{
    private static readonly SessionState[] VerifiableStates = [SessionState.IdentityVerification, SessionState.Disconnected];

    public async Task<SessionConsentStatus> RecordConsentAsync(
        Guid sessionId, Guid candidateId, bool accepted, string? textVersion, CancellationToken ct = default)
    {
        if (!await OwnsSessionAsync(sessionId, candidateId, ct))
        {
            return SessionConsentStatus.SessionNotFound;
        }

        if (accepted && !await db.IsCurrentVersionAsync(ConsentType.Session, textVersion, ct))
        {
            return SessionConsentStatus.ConsentVersionMismatch;
        }

        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockExamSessionAsync(sessionId, ct);
        var session = await db.ExamSessions.FirstAsync(s => s.Id == sessionId, ct);

        if (session.State != SessionState.ConsentPending)
        {
            return SessionConsentStatus.InvalidState;
        }

        if (accepted)
        {
            if (!await db.ReferenceFaceTemplates.AnyAsync(t => t.UserId == candidateId, ct))
            {
                return SessionConsentStatus.NoReferencePhoto;
            }

            db.ConsentRecords.Add(new ConsentRecord
            {
                UserId = candidateId,
                ExamSessionId = sessionId,
                Type = ConsentType.Session,
                TextVersion = textVersion!,
                GrantedAt = now,
            });
            session.TransitionTo(SessionState.AgentPairing);
        }
        else
        {
            // Hak tüketilmiyor: AttemptUsedAt'e dokunulmuyor, iptal edilen oturum da tek oturum
            // indeksine sayılmıyor (K-11). Aday alternatif yönteme yönlendirilir.
            session.TransitionTo(SessionState.Cancelled);
            session.EndedAt = now;
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = candidateId,
            Action = accepted ? "consent.grant" : "consent.refuse",
            TargetType = nameof(ExamSession),
            TargetId = sessionId,
            NewValue = JsonSerializer.Serialize(new { type = nameof(ConsentType.Session), textVersion, state = session.State.ToString() }),
            OccurredAt = now,
        });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return accepted ? SessionConsentStatus.Accepted : SessionConsentStatus.Refused;
    }

    /// <summary>
    /// Kameradan gelen birkaç kareyle canlılık testi ve yüz eşleştirmesi. Başarılıysa oturum
    /// InProgress'e geçer; başarısızsa oturum olduğu adımda kalır, deneme VERIFICATION_FAILED
    /// olayı ve kanıt karesiyle kaydedilir.
    /// </summary>
    public async Task<VerifyResult> VerifyAsync(
        Guid sessionId, Guid candidateId, IReadOnlyList<byte[]> frames, CancellationToken ct = default)
    {
        if (frames.Count is < ImageFile.MinFrames or > ImageFile.MaxFrames || !frames.All(f => ImageFile.IsAcceptable(f, ImageFile.MaxFrameBytes)))
        {
            return new(VerifyStatus.InvalidFrames);
        }

        var snapshot = await db.ExamSessions.AsNoTracking()
            .Where(s => s.Id == sessionId && s.CandidateId == candidateId)
            .Select(s => new { s.State })
            .SingleOrDefaultAsync(ct);

        if (snapshot is null)
        {
            return new(VerifyStatus.SessionNotFound);
        }

        if (!VerifiableStates.Contains(snapshot.State))
        {
            return new(VerifyStatus.InvalidState);
        }

        var now = clock.GetUtcNow();
        if (await IsCoolingDownAsync(sessionId, now, ct))
        {
            return new(VerifyStatus.TooSoon);
        }

        var template = await db.ReferenceFaceTemplates.AsNoTracking().SingleOrDefaultAsync(t => t.UserId == candidateId, ct);
        if (template is null)
        {
            return new(VerifyStatus.NoReferencePhoto);
        }

        float[] reference;
        try
        {
            reference = FaceVector.FromBytes(encryptor.Decrypt(
                template.EncryptedVector, template.KeyVersion, BiometricEnrollmentService.TemplateContext(candidateId)));
        }
        catch (CryptographicException)
        {
            // Bozuk ya da başka bir adayın satırından kopyalanmış vektör; doğrulama yapılamaz.
            return new(VerifyStatus.NoReferencePhoto);
        }

        // Görüntü servisi çağrısı kilit dışında: birkaç saniye sürebilir ve bu sırada oturuma
        // gelen diğer yazmalar beklemesin. Durum aşağıda kilit altında yeniden kontrol ediliyor.
        // Geçti/kaldı kararı servisin; eşikler onun config.yaml dosyasında (S-19).
        var result = await faces.VerifyAsync(frames, reference, ct);

        StoredEvidence? stored = result.Passed ? null : await evidence.SaveAsync(frames[0], ct);
        VerifyResult outcome;
        try
        {
            outcome = await SaveAttemptAsync(sessionId, candidateId, result, stored, ct);
        }
        catch
        {
            if (stored is not null)
            {
                await evidence.DeleteAsync(stored.StorageKey, CancellationToken.None);
            }

            throw;
        }

        // Deneme kaydedilmediyse (durum değişti, bekleme süresi) kanıt karesi de tutulmuyor.
        if (stored is not null && outcome.Status is not (VerifyStatus.LivenessFailed or VerifyStatus.IdentityMismatch))
        {
            await evidence.DeleteAsync(stored.StorageKey, CancellationToken.None);
        }

        return outcome.Status == VerifyStatus.Passed
            ? await ContinueSessionAsync(sessionId, outcome.Kind!.Value, ct)
            : outcome;
    }

    /// <summary>
    /// Denemeyi kaydeder: Verification satırı, deneme sayacı ve denetim kaydı; başarısızsa
    /// VERIFICATION_FAILED olayı ve kanıt karesi. Oturumun durumuna burada dokunulmuyor.
    /// </summary>
    private async Task<VerifyResult> SaveAttemptAsync(
        Guid sessionId, Guid candidateId, FaceVerifyResult result, StoredEvidence? stored, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockExamSessionAsync(sessionId, ct);
        var session = await db.ExamSessions.Include(s => s.Exam).FirstAsync(s => s.Id == sessionId, ct);

        // Aynı anda gelen iki deneme: ikincisi birincinin sonucunu görüyor.
        if (!VerifiableStates.Contains(session.State))
        {
            return new(VerifyStatus.InvalidState);
        }

        if (await IsCoolingDownAsync(sessionId, now, ct))
        {
            return new(VerifyStatus.TooSoon);
        }

        // Kesintiden dönüşte de aynı doğrulama zorunlu; oturum hakkı yenilenmiyor (FR-26).
        var kind = session.State == SessionState.Disconnected ? VerificationKind.Reconnect : VerificationKind.Initial;
        var status = result.Passed ? VerifyStatus.Passed : FailureStatus(result.Reason);

        if (!result.Passed)
        {
            // Olay servisi oturumu kilitleyip yeniden yüklüyor; bu yüzden oturumdaki değişiklikler
            // bu çağrıdan sonra yapılıyor.
            var recorded = await events.RecordSystemEventAsync(sessionId, new SystemEvent(
                EventType.VERIFICATION_FAILED,
                now,
                Payload: JsonSerializer.Serialize(new
                {
                    kind = kind.ToString(),
                    reason = result.Reason,
                    livenessScore = Math.Round(result.LivenessScore, 4),
                    similarity = Math.Round(result.Similarity, 4),
                })), ct);

            if (recorded.Status != EventRecordStatus.Recorded)
            {
                throw new InvalidOperationException($"Doğrulama olayı kaydedilemedi: {recorded.Status}.");
            }

            db.EvidenceFrames.Add(new EvidenceFrame
            {
                EventId = recorded.Event!.Id,
                StorageKey = stored!.StorageKey,
                KeyVersion = stored.KeyVersion,
                CreatedAt = now,
                DeleteAfter = now.AddDays(session.Exam.RetentionDays),
            });
        }

        session.VerificationAttempts++;
        db.Verifications.Add(new Verification
        {
            ExamSessionId = sessionId,
            Kind = kind,
            LivenessScore = result.LivenessScore,
            Similarity = result.Similarity,
            Passed = result.Passed,
            CreatedAt = now,
        });

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = candidateId,
            Action = "verification.attempt",
            TargetType = nameof(ExamSession),
            TargetId = sessionId,
            NewValue = JsonSerializer.Serialize(new { kind = kind.ToString(), passed = result.Passed, reason = result.Reason }),
            OccurredAt = now,
        });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(status, kind);
    }

    /// <summary>
    /// Başarılı doğrulamadan sonra oturum E4'e devrediliyor: ilk doğrulamada sınav başlıyor (süre ve hak),
    /// kesintiden dönüşte kaldığı yerden devam ediyor ve kesinti CONNECTION_LOST olarak yazılıyor.
    /// </summary>
    /// <remarks>
    /// Deneme kaydından ayrı işlemde, çünkü ResumeAfterReconnectAsync kendi işlemini açıyor. Arada ikinci
    /// bir deneme giremiyor (bekleme süresi); oturum bu sırada kapandıysa geçiş reddediliyor.
    /// </remarks>
    private async Task<VerifyResult> ContinueSessionAsync(Guid sessionId, VerificationKind kind, CancellationToken ct)
    {
        SessionOperationResult result;
        try
        {
            result = kind == VerificationKind.Initial
                ? await sessions.BeginExamAsync(sessionId, ct)
                : await sessions.ResumeAfterReconnectAsync(sessionId, ct);
        }
        catch (InvalidSessionTransitionException)
        {
            return new(VerifyStatus.InvalidState, kind);
        }

        return new(result.Status == SessionOperationStatus.Success ? VerifyStatus.Passed : VerifyStatus.InvalidState, kind);
    }

    // Karede yüz bulunamaması ve çözülemeyen kare de adaya "Canlı yüz algılanamadı" olarak gösteriliyor
    // (UC-12, 4a); birden fazla yüz, eşleşmeme ve nedeni bilinmeyen ret "Kimlik doğrulanamadı" (4b).
    private static VerifyStatus FailureStatus(string? reason) =>
        reason is FaceIssue.LivenessFailed or FaceIssue.NoFace or FaceIssue.InvalidImage
            ? VerifyStatus.LivenessFailed
            : VerifyStatus.IdentityMismatch;

    private Task<bool> OwnsSessionAsync(Guid sessionId, Guid candidateId, CancellationToken ct) =>
        db.ExamSessions.AnyAsync(s => s.Id == sessionId && s.CandidateId == candidateId, ct);

    private Task<bool> IsCoolingDownAsync(Guid sessionId, DateTimeOffset now, CancellationToken ct)
    {
        var since = now.AddSeconds(-options.Value.VerificationCooldownSeconds);
        return db.Verifications.AnyAsync(v => v.ExamSessionId == sessionId && v.CreatedAt > since, ct);
    }
}
