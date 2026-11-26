namespace AIGuard.Application.Abstractions;

/// <summary>Telefon görevinin sonucu (docs/contracts.md 5. bölüm).</summary>
/// <param name="Confidence">Karedeki en yüksek telefon skoru (0–1).</param>
public sealed record PhoneReading(bool Detected, double Confidence);

/// <summary>Kimlik görevinin sonucu; eşik vision'da (S-19), <paramref name="Matched"/> onun kararı.</summary>
/// <param name="Similarity">Kosinüs benzerliği (−1…1).</param>
public sealed record IdentityReading(bool Matched, double Similarity);

/// <summary>
/// Bir karenin ölçümü. face ve pose her karede istendiği için <see cref="FaceCount"/> her zaman dolu;
/// yüz yoksa kafa yönü alanları <c>null</c>. İstenmeyen görevin alanı da <c>null</c>.
/// </summary>
/// <param name="EvidenceFrame">Karede şüpheli bir durum varsa gelen karenin kendisi; yoksa <c>null</c>.</param>
public sealed record FrameMeasurement(
    int FaceCount,
    double? Yaw,
    double? Pitch,
    bool? GazeAway,
    PhoneReading? Phone,
    IdentityReading? Identity,
    byte[]? EvidenceFrame);

public enum FrameAnalysisStatus
{
    /// <summary>Ölçüm geldi.</summary>
    Ok,

    /// <summary>422 INVALID_IMAGE: adayın karesi çözülemedi. Boşluk sayılmıyor, kare atlanıyor.</summary>
    InvalidImage,

    /// <summary>Servis ya da sözleşme sorunu: ulaşılamadı, zaman aşımı, 401, 503, VALIDATION_ERROR, eksik alan.</summary>
    Unavailable,
}

/// <param name="HttpStatus">Servis yanıt verdiyse HTTP durumu; ulaşılamadıysa <c>null</c>.</param>
/// <param name="Code">Hata kodu: servisin <c>error.code</c>'u ya da istemcinin koyduğu (UNREACHABLE, TIMEOUT, INVALID_RESPONSE …).</param>
public sealed record FrameAnalysisResult(
    FrameAnalysisStatus Status, FrameMeasurement? Measurement = null, int? HttpStatus = null, string? Code = null)
{
    public static FrameAnalysisResult Ok(FrameMeasurement measurement) => new(FrameAnalysisStatus.Ok, measurement);

    public static FrameAnalysisResult InvalidImage() => new(FrameAnalysisStatus.InvalidImage, HttpStatus: 422, Code: FaceIssue.InvalidImage);

    public static FrameAnalysisResult Unavailable(int? httpStatus, string? code) => new(FrameAnalysisStatus.Unavailable, null, httpStatus, code);
}

/// <summary>Görüntü servisinin <c>/internal/analyze/frame</c> ucu (D4). Durumsuz; kurallar API'de (S-20).</summary>
/// <remarks>
/// face ve pose her istekte. <paramref name="referenceVector"/> verilirse identity de isteniyor (S-21).
/// Servis sorunları istisna değil <see cref="FrameAnalysisStatus.Unavailable"/> olarak dönüyor; yalnızca
/// <paramref name="ct"/> iptal edilirse <see cref="OperationCanceledException"/>.
/// </remarks>
public interface IFrameAnalysisService
{
    Task<FrameAnalysisResult> AnalyzeAsync(
        Guid sessionId, byte[] frame, DateTimeOffset ts, bool phone, float[]? referenceVector, CancellationToken ct = default);
}
