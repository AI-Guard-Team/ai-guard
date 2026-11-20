using AIGuard.Application.Abstractions;
using AIGuard.Application.Biometrics;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using static AIGuard.IntegrationTests.Biometrics.BiometricTestHelpers;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.Biometrics;

public class SessionVerificationServiceTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private void RequireDb() => Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

    [SkippableFact]
    public async Task Consent_without_a_reference_photo_does_not_start_the_flow()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await PendingSessionAsync(db, SessionState.ConsentPending, enrolled: false);
        var service = Verification(db, new FakeFaceService(), new MemoryEvidenceStore(), Clock());

        var status = await service.RecordConsentAsync(session.Id, session.CandidateId, true, Settings.SessionConsentTextVersion);
        await db.Entry(session).ReloadAsync();

        Assert.Equal(SessionConsentStatus.NoReferencePhoto, status);
        Assert.Equal(SessionState.ConsentPending, session.State);
    }

    [SkippableFact]
    public async Task Accepted_consent_moves_to_agent_pairing_and_refusal_cancels_without_using_the_attempt()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var accepted = await PendingSessionAsync(db, SessionState.ConsentPending);
        var refused = await PendingSessionAsync(db, SessionState.ConsentPending);
        var service = Verification(db, new FakeFaceService(), new MemoryEvidenceStore(), Clock());

        await service.RecordConsentAsync(accepted.Id, accepted.CandidateId, true, Settings.SessionConsentTextVersion);
        await service.RecordConsentAsync(refused.Id, refused.CandidateId, false, null);
        await db.Entry(accepted).ReloadAsync();
        await db.Entry(refused).ReloadAsync();

        Assert.Equal(SessionState.AgentPairing, accepted.State);
        Assert.True(await db.ConsentRecords.AnyAsync(c => c.ExamSessionId == accepted.Id && c.Type == ConsentType.Session));
        Assert.Equal(SessionState.Cancelled, refused.State);
        Assert.Null((await db.ExamAssignments.SingleAsync(a => a.UserId == refused.CandidateId)).AttemptUsedAt);
    }

    [SkippableFact]
    public async Task Successful_verification_starts_the_exam_and_uses_the_attempt()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await PendingSessionAsync(db, SessionState.IdentityVerification);

        var result = await Verification(db, new FakeFaceService(), new MemoryEvidenceStore(), Clock())
            .VerifyAsync(session.Id, session.CandidateId, Frames());
        await db.Entry(session).ReloadAsync();

        Assert.Equal(new VerifyResult(VerifyStatus.Passed, VerificationKind.Initial), result);
        Assert.Equal(SessionState.InProgress, session.State);
        Assert.Equal(Now, session.StartedAt);
        Assert.Equal(Now.AddMinutes(30), session.EndsAt);
        Assert.Equal(Now, (await db.ExamAssignments.SingleAsync(a => a.UserId == session.CandidateId)).AttemptUsedAt);
        Assert.True(await db.Verifications.AnyAsync(v => v.ExamSessionId == session.Id && v.Passed));
    }

    [SkippableTheory]
    [InlineData("LIVENESS_FAILED", VerifyStatus.LivenessFailed)]    // ekrandan gösterilen fotoğraf
    [InlineData("NO_FACE", VerifyStatus.LivenessFailed)]            // kamerada yüz yok
    [InlineData("IDENTITY_MISMATCH", VerifyStatus.IdentityMismatch)] // başka birinin yüzü
    [InlineData("MULTIPLE_FACES", VerifyStatus.IdentityMismatch)]
    public async Task Failed_attempt_is_blocked_and_recorded_with_an_evidence_frame(string reason, VerifyStatus expected)
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await PendingSessionAsync(db, SessionState.IdentityVerification);
        var store = new MemoryEvidenceStore();
        var faces = new FakeFaceService { VerifyResult = Failed(reason) };

        var result = await Verification(db, faces, store, Clock()).VerifyAsync(session.Id, session.CandidateId, Frames());
        await db.Entry(session).ReloadAsync();

        var failed = await db.Events.Include(e => e.EvidenceFrame).SingleAsync(e => e.ExamSessionId == session.Id);
        Assert.Equal(expected, result.Status);
        Assert.Equal(SessionState.IdentityVerification, session.State);
        Assert.Equal(1, session.VerificationAttempts);
        Assert.Equal(EventType.VERIFICATION_FAILED, failed.Type);
        Assert.Contains(reason, failed.Payload);
        Assert.Equal(Now.AddDays(30), failed.EvidenceFrame!.DeleteAfter);
        Assert.True(store.Files.ContainsKey(failed.EvidenceFrame.StorageKey));
        Assert.Null(session.StartedAt);
    }

    [SkippableFact]
    public async Task Attempts_are_throttled_but_the_candidate_can_retry()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await PendingSessionAsync(db, SessionState.IdentityVerification);
        var clock = Clock();
        var faces = new FakeFaceService { VerifyResult = Failed("IDENTITY_MISMATCH") };
        var service = Verification(db, faces, new MemoryEvidenceStore(), clock);

        await service.VerifyAsync(session.Id, session.CandidateId, Frames());
        var tooSoon = await service.VerifyAsync(session.Id, session.CandidateId, Frames());
        clock.Advance(TimeSpan.FromSeconds(Settings.VerificationCooldownSeconds + 1));
        faces.VerifyResult = new FaceVerifyResult(0.95, 0.8, true);
        var retry = await service.VerifyAsync(session.Id, session.CandidateId, Frames());

        Assert.Equal(VerifyStatus.TooSoon, tooSoon.Status);
        Assert.Equal(VerifyStatus.Passed, retry.Status);
    }

    [SkippableFact]
    public async Task Reconnect_verification_resumes_without_restarting_the_exam()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await PendingSessionAsync(db, SessionState.IdentityVerification);
        var clock = Clock();
        var service = Verification(db, new FakeFaceService(), new MemoryEvidenceStore(), clock);
        await service.VerifyAsync(session.Id, session.CandidateId, Frames());

        await db.Entry(session).ReloadAsync();
        session.State = SessionState.Disconnected; // kurulum: zamanlayıcı (E4) kesintiyi böyle işaretliyor
        session.DisconnectedAt = Now.AddMinutes(1);
        await db.SaveChangesAsync();
        clock.Advance(TimeSpan.FromMinutes(3));

        var result = await service.VerifyAsync(session.Id, session.CandidateId, Frames());
        await db.Entry(session).ReloadAsync();

        var lost = await db.Events.SingleAsync(e => e.ExamSessionId == session.Id && e.Type == EventType.CONNECTION_LOST);
        Assert.Equal(new VerifyResult(VerifyStatus.Passed, VerificationKind.Reconnect), result);
        Assert.Equal(SessionState.InProgress, session.State);
        Assert.Equal(Now, session.StartedAt); // süre baştan başlamıyor
        Assert.Equal(120, lost.DurationSec);  // kesinti süresi E4 tarafından yazılıyor
    }

    [SkippableFact]
    public async Task Candidate_cannot_verify_another_candidates_session_or_use_a_copied_vector()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var mine = await PendingSessionAsync(db, SessionState.IdentityVerification);
        var other = await PendingSessionAsync(db, SessionState.IdentityVerification);
        var service = Verification(db, new FakeFaceService(), new MemoryEvidenceStore(), Clock());

        var foreign = await service.VerifyAsync(other.Id, mine.CandidateId, Frames());

        // Veritabanında bir adayın şifreli vektörü diğerininkinin üzerine kopyalanırsa açılamamalı.
        var myTemplate = await db.ReferenceFaceTemplates.SingleAsync(t => t.UserId == mine.CandidateId);
        var otherTemplate = await db.ReferenceFaceTemplates.AsNoTracking().SingleAsync(t => t.UserId == other.CandidateId);
        myTemplate.EncryptedVector = otherTemplate.EncryptedVector;
        await db.SaveChangesAsync();
        var copied = await service.VerifyAsync(mine.Id, mine.CandidateId, Frames());

        Assert.Equal(VerifyStatus.SessionNotFound, foreign.Status);
        Assert.Equal(VerifyStatus.NoReferencePhoto, copied.Status);
    }

    [SkippableFact]
    public async Task Reconnect_after_the_exam_time_ran_out_does_not_resume()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await PendingSessionAsync(db, SessionState.IdentityVerification);
        var clock = Clock();
        var service = Verification(db, new FakeFaceService(), new MemoryEvidenceStore(), clock);
        await service.VerifyAsync(session.Id, session.CandidateId, Frames());

        await db.Entry(session).ReloadAsync();
        session.State = SessionState.Disconnected;
        await db.SaveChangesAsync();
        clock.Advance(TimeSpan.FromMinutes(31)); // sınav 30 dk

        var result = await service.VerifyAsync(session.Id, session.CandidateId, Frames());
        await db.Entry(session).ReloadAsync();

        Assert.Equal(VerifyStatus.InvalidState, result.Status);
        Assert.Equal(SessionState.Disconnected, session.State); // otomatik teslimi zamanlayıcı yapıyor
    }

    [SkippableFact]
    public async Task Evidence_is_not_kept_when_the_session_closed_while_the_vision_service_was_answering()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await PendingSessionAsync(db, SessionState.IdentityVerification);
        var store = new MemoryEvidenceStore();
        var faces = new FakeFaceService
        {
            VerifyResult = Failed(FaceIssue.IdentityMismatch),
            // Görüntü servisi yanıt verirken aday başka sekmeden rızayı geri aldı ya da oturum kapandı.
            DuringVerify = async () =>
            {
                await using var other = fx.CreateContext();
                await other.ExamSessions.Where(s => s.Id == session.Id)
                    .ExecuteUpdateAsync(u => u.SetProperty(s => s.State, SessionState.Cancelled));
                db.ChangeTracker.Clear(); // gerçek istekte her istek yeni bağlamla geliyor
            },
        };

        var result = await Verification(db, faces, store, Clock()).VerifyAsync(session.Id, session.CandidateId, Frames());

        Assert.Equal(VerifyStatus.InvalidState, result.Status);
        Assert.Empty(store.Files); // diske yazılan kare geri silindi (KVKK)
        Assert.False(await db.Events.AnyAsync(e => e.ExamSessionId == session.Id));
        Assert.False(await db.Verifications.AnyAsync(v => v.ExamSessionId == session.Id));
    }

    [SkippableFact]
    public async Task Unavailable_vision_service_does_not_count_as_an_attempt()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await PendingSessionAsync(db, SessionState.IdentityVerification);
        var store = new MemoryEvidenceStore();
        var service = Verification(db, new FakeFaceService { Unavailable = true }, store, Clock());

        await Assert.ThrowsAsync<FaceServiceUnavailableException>(
            () => service.VerifyAsync(session.Id, session.CandidateId, Frames()));
        await db.Entry(session).ReloadAsync();

        Assert.Equal(0, session.VerificationAttempts);
        Assert.Empty(store.Files);
        Assert.False(await db.Events.AnyAsync(e => e.ExamSessionId == session.Id));
    }

    [SkippableFact]
    public async Task Too_few_frames_are_rejected_before_calling_the_vision_service()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await PendingSessionAsync(db, SessionState.IdentityVerification);
        var faces = new FakeFaceService();

        var result = await Verification(db, faces, new MemoryEvidenceStore(), Clock())
            .VerifyAsync(session.Id, session.CandidateId, [Jpeg(), Jpeg()]);

        Assert.Equal(VerifyStatus.InvalidFrames, result.Status);
        Assert.Equal(0, faces.Calls);
    }
}
