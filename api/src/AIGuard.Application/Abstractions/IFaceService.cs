namespace AIGuard.Application.Abstractions;

/// <summary>Görüntü servisinin yüz uçlarındaki neden kodları (docs/contracts.md 1., 3. ve 4. bölüm).</summary>
/// <remarks>
/// Kodlar tek yerde tutuluyor: istemci (Infrastructure) bunlara göre ayrıştırıyor, uçlar (Api) adaya
/// hangi mesajın gideceğini bunlara göre seçiyor. Sözleşmeye yeni kod eklenirse önce burası değişmeli.
/// </remarks>
public static class FaceIssue
{
    public const string NoFace = "NO_FACE";
    public const string MultipleFaces = "MULTIPLE_FACES";
    public const string FaceTooSmall = "FACE_TOO_SMALL";
    public const string ImageTooBlurry = "IMAGE_TOO_BLURRY";
    public const string InvalidImage = "INVALID_IMAGE";
    public const string LivenessFailed = "LIVENESS_FAILED";
    public const string IdentityMismatch = "IDENTITY_MISMATCH";

    /// <summary>
    /// Kayıt fotoğrafının reddedilme nedenleri; adayın fotoğrafıyla ilgili. Bunların dışındaki 422
    /// (ör. VALIDATION_ERROR) isteğin sözleşmeye uymadığı anlamına geliyor ve adaya yüklenmiyor.
    /// </summary>
    public static readonly IReadOnlySet<string> EnrollRejections =
        new HashSet<string> { NoFace, MultipleFaces, FaceTooSmall, ImageTooBlurry, InvalidImage };
}

/// <param name="Vector">Referans yüz vektörü; fotoğraf uygun değilse boş.</param>
/// <param name="Issue">Fotoğraf reddedildiyse nedeni (<see cref="FaceIssue.EnrollRejections"/>).</param>
public sealed record FaceEnrollResult(float[]? Vector, string? Issue)
{
    public bool Rejected => Vector is null;

    public static FaceEnrollResult Ok(float[] vector) => new(vector, null);

    public static FaceEnrollResult Rejection(string issue) => new(null, issue);
}

/// <summary>Görüntü servisinin doğrulama sonucu; geçti/kaldı kararı serviste (S-19).</summary>
/// <param name="Reason">Başarısızlık nedeni: LIVENESS_FAILED, IDENTITY_MISMATCH, NO_FACE, MULTIPLE_FACES; kare çözülemediyse INVALID_IMAGE.</param>
public sealed record FaceVerifyResult(double LivenessScore, double Similarity, bool Passed, string? Reason = null);

/// <summary>Görüntü servisinin yüz uçları (D2).</summary>
/// <remarks>
/// Görüntüler yalnızca bellekte taşınır; bu arayüzün uygulamaları diske yazmamalı.
/// Servise ulaşılamazsa veya yanıt sözleşmeye uymuyorsa <see cref="FaceServiceUnavailableException"/>:
/// bu durumda adayın denemesi kaydedilmiyor, aday daha sonra tekrar deniyor.
/// </remarks>
public interface IFaceService
{
    Task<FaceEnrollResult> EnrollAsync(byte[] image, CancellationToken ct = default);

    Task<FaceVerifyResult> VerifyAsync(IReadOnlyList<byte[]> frames, float[] referenceVector, CancellationToken ct = default);
}

public sealed class FaceServiceUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
