using System.Security.Cryptography;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Biometrics;
using AIGuard.Application.Sessions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Persistence;
using AIGuard.Infrastructure.Security;
using Microsoft.Extensions.Time.Testing;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.Biometrics;

/// <summary>Görüntü servisinin yerine geçen sahte servis (D2 gelene kadar ve testlerde).</summary>
internal sealed class FakeFaceService : IFaceService
{
    public FaceEnrollResult EnrollResult { get; set; } = FaceEnrollResult.Ok(BiometricTestHelpers.Vector(1));

    public FaceVerifyResult VerifyResult { get; set; } = new(0.95, 0.8, true);

    public int Calls { get; private set; }

    /// <summary>Servis yanıt verirken araya giren işlem (ör. oturumun başka bir istekle kapanması).</summary>
    public Func<Task>? DuringVerify { get; set; }

    /// <summary>Servise ulaşılamıyor.</summary>
    public bool Unavailable { get; set; }

    public Task<FaceEnrollResult> EnrollAsync(byte[] image, CancellationToken ct = default)
    {
        Calls++;
        return Unavailable ? throw new FaceServiceUnavailableException("test") : Task.FromResult(EnrollResult);
    }

    public async Task<FaceVerifyResult> VerifyAsync(IReadOnlyList<byte[]> frames, float[] referenceVector, CancellationToken ct = default)
    {
        Calls++;
        if (Unavailable)
        {
            throw new FaceServiceUnavailableException("test");
        }

        if (DuringVerify is not null)
        {
            await DuringVerify();
        }

        return VerifyResult;
    }
}

internal sealed class MemoryEvidenceStore : IEvidenceStore
{
    public Dictionary<string, byte[]> Files { get; } = new();

    public Task<StoredEvidence> SaveAsync(byte[] image, CancellationToken ct = default)
    {
        var key = $"{Guid.NewGuid():N}.bin";
        Files[key] = image;
        return Task.FromResult(new StoredEvidence(key, 1));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        Files.Remove(storageKey);
        return Task.CompletedTask;
    }

    public Task<byte[]?> ReadAsync(string storageKey, int keyVersion, CancellationToken ct = default) =>
        Task.FromResult(Files.GetValueOrDefault(storageKey));
}

internal static class BiometricTestHelpers
{
    public static readonly AesGcmFieldEncryptor Encryptor = new(RandomNumberGenerator.GetBytes(32));

    public static readonly BiometricOptions Settings = new();

    public static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

    /// <summary>Doğrulamada gönderilen kareler; sözleşmede en az 3 kare (docs/contracts.md).</summary>
    public static byte[][] Frames() => [Jpeg(), Jpeg(), Jpeg()];

    public static FaceVerifyResult Failed(string reason) => new(0.3, 0.1, false, reason);

    /// <summary>Birim uzunlukta, değere göre farklı bir vektör (sözleşme: L2 ile normalize).</summary>
    public static float[] Vector(float value)
    {
        var raw = Enumerable.Range(0, FaceVector.Dimension).Select(i => Math.Sin(i + value)).ToArray();
        var norm = Math.Sqrt(raw.Sum(v => v * v));
        return raw.Select(v => (float)(v / norm)).ToArray();
    }

    public static BiometricEnrollmentService Enrollment(AppDbContext db, IFaceService faces, TimeProvider? clock = null) =>
        new(db, faces, Encryptor, clock ?? Clock());

    public static SessionVerificationService Verification(
        AppDbContext db, IFaceService faces, IEvidenceStore store, FakeTimeProvider clock)
    {
        var events = Service(db, clock);
        return new(db, faces, Encryptor, store, events, new SessionService(db, events, clock),
            Microsoft.Extensions.Options.Options.Create(Settings), clock);
    }

    /// <summary>Atanmış aday ve verilen adımda bekleyen, henüz başlamamış oturum.</summary>
    public static async Task<ExamSession> PendingSessionAsync(AppDbContext db, SessionState state, bool enrolled = true)
    {
        var exam = TestData.Exam();
        var candidate = TestData.Candidate();
        var session = new ExamSession { Exam = exam, Candidate = candidate, CreatedAt = Now };
        db.ExamSessions.Add(session);
        db.ExamAssignments.Add(new ExamAssignment { Exam = exam, User = candidate, Role = AssignmentRole.Candidate });
        await db.SaveChangesAsync();

        // Fotoğraf, oturum doğrulama adımına gelmeden yükleniyor (sonrasında değiştirilemiyor).
        if (enrolled)
        {
            var result = await Enrollment(db, new FakeFaceService()).EnrollAsync(
                candidate.Id, true, ConsentText.InitialBiometricVersion, Jpeg());
            Assert.Equal(EnrollStatus.Enrolled, result.Status);
        }

        session.State = state;
        await db.SaveChangesAsync();
        return session;
    }
}
