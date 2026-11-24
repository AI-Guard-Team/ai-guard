using System.Security.Cryptography;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Review;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Api.Controllers;

/// <summary>Kanıt karesi (A4, K-31): yalnızca inceleme detay ucunun ürettiği kısa ömürlü imzalı bağlantıyla.</summary>
[ApiController]
public sealed class EvidenceController(IAppDbContext db, IEvidenceStore store, EvidenceLinkSigner links, ILogger<EvidenceController> logger)
    : ControllerBase
{
    /// <summary>
    /// Şifresi çözülmüş kare. Bağlantı 5 dakika geçerli; imzası bozuk ya da süresi dolmuş bağlantı 404.
    /// </summary>
    /// <remarks>
    /// [AllowAnonymous] gerekçesi (K-20): panel kareyi &lt;img src&gt; ile açıyor ve tarayıcı bu istekte
    /// Authorization başlığı göndermiyor. Yetki kontrolü bağlantıyı üreten detay ucunda yapılıyor; imza
    /// kare kimliğine ve son tarihe bağlı olduğu için bağlantı başka bir kareye ya da daha uzun süreye
    /// çevrilemiyor.
    /// </remarks>
    [HttpGet("api/evidence/{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, [FromQuery] long exp, [FromQuery] string? sig, CancellationToken ct)
    {
        // Geçersiz bağlantıda karenin var olup olmadığı da söylenmiyor.
        if (!links.IsValid(id, exp, sig))
        {
            return NotFound();
        }

        var frame = await db.EvidenceFrames.AsNoTracking().SingleOrDefaultAsync(f => f.Id == id, ct);
        if (frame is null)
        {
            return NotFound();
        }

        byte[]? image;
        try
        {
            image = await store.ReadAsync(frame.StorageKey, frame.KeyVersion, ct);
        }
        catch (CryptographicException ex)
        {
            // Dosya bozulmuş ya da başka bir dosyayla değiştirilmiş (GCM etiketi tutmuyor): bütünlük sorunu,
            // incelenmesi için loga yazılıyor; girişsiz uçtan ayrıntı dönmüyor.
            logger.LogWarning(ex, "Kanıt karesi çözülemedi (kare {FrameId}); dosya bozulmuş ya da değiştirilmiş olabilir.", id);
            return NotFound();
        }

        if (image is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(image, ContentType(image));
    }

    // Kamera kareleri JPEG; doğrulama karelerinde PNG ya da WebP de olabilir (K-27).
    private static string ContentType(byte[] data) =>
        data.AsSpan().StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47]) ? "image/png"
        : data.Length >= 12 && data.AsSpan(8, 4).SequenceEqual("WEBP"u8) ? "image/webp"
        : "image/jpeg";
}
