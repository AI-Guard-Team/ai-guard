using AIGuard.Application.Biometrics;
using AIGuard.Application.ConsentTexts;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.IntegrationTests.Biometrics;
using Microsoft.Extensions.Time.Testing;
using static AIGuard.IntegrationTests.Biometrics.BiometricTestHelpers;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.DataProtection;

/// <summary>
/// A5: rıza metni yeni sürümle değişince eski sürümle verilen rıza kabul edilmiyor (K-32). Yeni sürüm bu sınıfın
/// veritabanındaki güncel metni değiştirdiği için ayrı sınıfta (ayrı veritabanı).
/// </summary>
public class ConsentTextVersioningTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    [SkippableFact]
    public async Task A_new_version_replaces_the_old_one_for_new_consents()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        db.ConsentTexts.AddRange(
            new ConsentText { Type = ConsentType.Biometric, Version = "kvkk-biometric-v2", Title = "v2", Body = "Yeni metin", PublishedAt = DateTimeOffset.UtcNow },
            new ConsentText { Type = ConsentType.Session, Version = "kvkk-session-v2", Title = "v2", Body = "Yeni metin", PublishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        Assert.Equal("kvkk-biometric-v2", (await new ConsentTextService(db).GetCurrentAsync(ConsentType.Biometric))?.Version);

        // Kayıt fotoğrafı: eski metinle reddediliyor, yenisiyle kabul.
        var candidate = TestData.Candidate();
        db.Users.Add(candidate);
        await db.SaveChangesAsync();
        var enrollment = Enrollment(db, new FakeFaceService());
        Assert.Equal(EnrollStatus.ConsentVersionMismatch,
            (await enrollment.EnrollAsync(candidate.Id, true, ConsentText.InitialBiometricVersion, Jpeg())).Status);
        Assert.Equal(EnrollStatus.Enrolled, (await enrollment.EnrollAsync(candidate.Id, true, "kvkk-biometric-v2", Jpeg())).Status);

        // Oturum rızası da aynı kural.
        var session = await PendingSessionAsync(db, SessionState.ConsentPending, enrolled: false);
        var verification = Verification(db, new FakeFaceService(), new MemoryEvidenceStore(), new FakeTimeProvider(Now));
        Assert.Equal(SessionConsentStatus.ConsentVersionMismatch,
            await verification.RecordConsentAsync(session.Id, session.CandidateId, true, ConsentText.InitialSessionVersion));

        // Eski metin, adayın geçmişte kabul ettiğini göstermek için okunabilir kalıyor.
        Assert.NotNull(await new ConsentTextService(db).GetAsync(ConsentType.Biometric, ConsentText.InitialBiometricVersion));
    }
}
