using AIGuard.Api.Auth;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Biometrics;
using AIGuard.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

/// <summary>Kayıt fotoğrafı formu (multipart).</summary>
public sealed class PhotoUploadForm
{
    /// <summary>JPEG, PNG veya WebP, en fazla 5 MB.</summary>
    public IFormFile? Photo { get; init; }

    /// <summary>Biyometrik veri için ayrı açık rıza; true olmalı.</summary>
    public bool Consent { get; init; }

    /// <summary>Adaya gösterilen rıza metninin sürümü.</summary>
    public string? ConsentTextVersion { get; init; }
}

/// <summary>Adayın kayıt fotoğrafı ve biyometrik rızası (A3).</summary>
[ApiController]
[Route("api/me")]
// Kart: yalnızca Candidate ve yalnızca kendi kaydı; kimlik JWT'den geliyor, istekte kullanıcı kimliği yok.
[Authorize(Roles = nameof(UserRole.Candidate))]
public sealed class BiometricsController(BiometricEnrollmentService enrollment) : ControllerBase
{
    // Fotoğraf + iki kısa alan. Çok parçalı gövdenin diske tamponlanmaması için eşik dosya
    // sınırının üzerinde tutuluyor: ham fotoğraf geçici dosyaya da yazılmasın.
    private const int MaxBody = ImageFile.MaxPhotoBytes + 64 * 1024;

    [HttpPost("photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxBody)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBody, MemoryBufferThreshold = MaxBody)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadPhoto([FromForm] PhotoUploadForm form, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var photo = await FormFiles.ReadAsync(form.Photo, ImageFile.MaxPhotoBytes, ct);
        if (photo is null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Fotoğraf yok ya da 5 MB'tan büyük.");
        }

        EnrollResult result;
        try
        {
            result = await enrollment.EnrollAsync(userId, form.Consent, form.ConsentTextVersion, photo, ct);
        }
        catch (FaceServiceUnavailableException)
        {
            return VisionUnavailable();
        }

        return result.Status switch
        {
            EnrollStatus.Enrolled => NoContent(),
            EnrollStatus.ConsentRequired => Problem(statusCode: 400, title: "Biyometrik veri için açık rıza gerekli."),
            EnrollStatus.ConsentVersionMismatch => Problem(statusCode: 400, title: "Rıza metni güncel değil; sayfayı yenileyip tekrar onaylayın."),
            EnrollStatus.InvalidImage => Problem(statusCode: 400, title: "Fotoğraf JPEG, PNG veya WebP olmalı."),
            EnrollStatus.NoUsableFace => Problem(statusCode: 422, title: "Fotoğrafta tek ve net bir yüz bulunamadı.",
                detail: PhotoHint(result.FaceIssue)),
            EnrollStatus.NotCandidate => Problem(statusCode: 403, title: "Kayıt fotoğrafını yalnızca adaylar yükleyebilir."),
            _ => Problem(statusCode: 409, title: "Süren bir sınav oturumu varken kayıt fotoğrafı değiştirilemez."),
        };
    }

    /// <summary>Biyometrik rızayı geri alır; yüz vektörü silinir, açık oturumlar iptal edilir (FR-05).</summary>
    [HttpDelete("biometric-consent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> WithdrawConsent(CancellationToken ct)
    {
        var userId = User.GetUserId();

        var result = await enrollment.WithdrawConsentAsync(userId, ct);
        return result.UserFound ? NoContent() : Unauthorized();
    }

    // Adaya neyi düzeltmesi gerektiği söyleniyor (S2: her hatada neden ve çözüm). Bilinmeyen kodda genel öneri.
    private static string PhotoHint(string? issue) => issue switch
    {
        FaceIssue.NoFace => "Yüzünüzün açıkça göründüğü bir fotoğraf yükleyin.",
        FaceIssue.MultipleFaces => "Fotoğrafta yalnızca siz olmalısınız.",
        FaceIssue.FaceTooSmall => "Yüzünüz fotoğrafta çok küçük; daha yakından çekilmiş bir fotoğraf yükleyin.",
        FaceIssue.ImageTooBlurry => "Fotoğraf bulanık; iyi ışıkta çekilmiş net bir fotoğraf yükleyin.",
        FaceIssue.InvalidImage => "Fotoğraf açılamadı; başka bir dosya deneyin.",
        _ => "Yüzünüzün önden ve net göründüğü, yalnızca sizin olduğunuz bir fotoğraf yükleyin.",
    };

    private ObjectResult VisionUnavailable() =>
        Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Görüntü servisine şu an ulaşılamıyor; biraz sonra tekrar deneyin.");
}
