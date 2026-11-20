using AIGuard.Application.Abstractions;
using AIGuard.Application.Biometrics;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using static AIGuard.IntegrationTests.Biometrics.BiometricTestHelpers;

namespace AIGuard.IntegrationTests.Biometrics;

public class BiometricEnrollmentServiceTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private void RequireDb() => Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

    [SkippableFact]
    public async Task Photo_is_turned_into_an_encrypted_vector_with_a_consent_record()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var candidate = TestData.Candidate();
        db.Users.Add(candidate);
        await db.SaveChangesAsync();
        var photo = Jpeg();

        var status = await Enrollment(db, new FakeFaceService()).EnrollAsync(candidate.Id, true, Settings.BiometricConsentTextVersion, photo);

        var template = await db.ReferenceFaceTemplates.AsNoTracking().SingleAsync(t => t.UserId == candidate.Id);
        var consent = await db.ConsentRecords.AsNoTracking().SingleAsync(c => c.Id == template.ConsentRecordId);
        var plain = Encryptor.Decrypt(template.EncryptedVector, template.KeyVersion, BiometricEnrollmentService.TemplateContext(candidate.Id));

        Assert.Equal(EnrollStatus.Enrolled, status.Status);
        Assert.Equal(Vector(1), FaceVector.FromBytes(plain));
        Assert.Equal((ConsentType.Biometric, Settings.BiometricConsentTextVersion), (consent.Type, consent.TextVersion));
        Assert.All(photo, b => Assert.Equal(0, b)); // ham fotoğraf bellekte de bırakılmıyor
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "biometric.enroll" && a.ActorUserId == candidate.Id));
    }

    [SkippableTheory]
    [InlineData(false, "kvkk-biometric-v1", true, UserRole.Candidate, EnrollStatus.ConsentRequired)]
    [InlineData(true, "kvkk-biometric-v0", true, UserRole.Candidate, EnrollStatus.ConsentVersionMismatch)]
    [InlineData(true, "kvkk-biometric-v1", false, UserRole.Candidate, EnrollStatus.InvalidImage)]
    [InlineData(true, "kvkk-biometric-v1", true, UserRole.Reviewer, EnrollStatus.NotCandidate)]
    public async Task Invalid_requests_never_reach_the_vision_service(
        bool consent, string version, bool jpeg, UserRole role, EnrollStatus expected)
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var user = TestData.Candidate();
        user.Role = role;
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var faces = new FakeFaceService();

        var status = await Enrollment(db, faces).EnrollAsync(user.Id, consent, version, jpeg ? Jpeg() : "GIF89a"u8.ToArray());

        Assert.Equal(expected, status.Status);
        Assert.Equal(0, faces.Calls);
        Assert.False(await db.ConsentRecords.AnyAsync(c => c.UserId == user.Id));
    }

    [SkippableFact]
    public async Task Photo_without_a_single_clear_face_stores_nothing()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var candidate = TestData.Candidate();
        db.Users.Add(candidate);
        await db.SaveChangesAsync();
        var faces = new FakeFaceService { EnrollResult = FaceEnrollResult.Rejection(FaceIssue.MultipleFaces) };

        var status = await Enrollment(db, faces).EnrollAsync(candidate.Id, true, Settings.BiometricConsentTextVersion, Jpeg());

        Assert.Equal(new EnrollResult(EnrollStatus.NoUsableFace, FaceIssue.MultipleFaces), status);
        Assert.False(await db.ReferenceFaceTemplates.AnyAsync(t => t.UserId == candidate.Id));
        Assert.False(await db.ConsentRecords.AnyAsync(c => c.UserId == candidate.Id));
    }

    [SkippableFact]
    public async Task Reference_photo_cannot_be_replaced_during_an_exam()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await PendingSessionAsync(db, SessionState.InProgress);

        var status = await Enrollment(db, new FakeFaceService { EnrollResult = FaceEnrollResult.Ok(Vector(2)) })
            .EnrollAsync(session.CandidateId, true, Settings.BiometricConsentTextVersion, Jpeg());

        Assert.Equal(EnrollStatus.SessionActive, status.Status);
    }

    [SkippableFact]
    public async Task Withdrawing_consent_deletes_the_vector_and_cancels_open_sessions()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var running = await PendingSessionAsync(db, SessionState.InProgress);
        var candidateId = running.CandidateId;

        var result = await Enrollment(db, new FakeFaceService()).WithdrawConsentAsync(candidateId);
        await db.Entry(running).ReloadAsync();

        Assert.Equal(new WithdrawResult(true, 1), result);
        Assert.Equal(SessionState.Cancelled, running.State);
        Assert.False(await db.ReferenceFaceTemplates.AnyAsync(t => t.UserId == candidateId));
        Assert.True(await db.ConsentRecords.Where(c => c.UserId == candidateId).AllAsync(c => c.WithdrawnAt != null));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "consent.withdraw" && a.TargetId == candidateId));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "session.cancel" && a.TargetId == running.Id));
    }

    [SkippableFact]
    public async Task Session_in_agent_pairing_stays_but_can_no_longer_be_verified_after_withdrawal()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var pairing = await PendingSessionAsync(db, SessionState.AgentPairing);

        // Durum makinesinde AgentPairing → Cancelled geçişi yok (S-23); oturum olduğu yerde kalıyor.
        var result = await Enrollment(db, new FakeFaceService()).WithdrawConsentAsync(pairing.CandidateId);
        await db.Entry(pairing).ReloadAsync();
        Assert.Equal(new WithdrawResult(true, 0), result);
        Assert.Equal(SessionState.AgentPairing, pairing.State);

        // Ajan adımı geçilse bile vektör olmadığı için doğrulama yapılamıyor; aday sınava giremiyor.
        pairing.State = SessionState.IdentityVerification; // kurulum: Y2/Y4'ün geçişi
        await db.SaveChangesAsync();
        var verify = await Verification(db, new FakeFaceService(), new MemoryEvidenceStore(), Events.EventTestHelpers.Clock())
            .VerifyAsync(pairing.Id, pairing.CandidateId, Frames());

        Assert.Equal(VerifyStatus.NoReferencePhoto, verify.Status);
    }
}
