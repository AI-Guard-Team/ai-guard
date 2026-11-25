using AIGuard.Api.Auth;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Biometrics;
using AIGuard.Application.Events;
using AIGuard.Application.Sessions;
using AIGuard.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

/// <param name="Accepted">Zorunlu. false ise oturum iptal edilir (hak tüketilmez).</param>
/// <param name="TextVersion">Adaya gösterilen rıza metninin sürümü; kabulde zorunlu.</param>
public sealed record SessionConsentRequest(bool? Accepted, string? TextVersion);

public sealed record SessionStateResponse(SessionState State);

/// <summary>Oturum rızası ve sınav başı doğrulama (A3).</summary>
[ApiController]
[Route("api/sessions/{sessionId:guid}")]
// Aday yalnızca kendi oturumuna erişiyor; başkasının oturumu 404 (K-20). Diğer oturum uçları gibi
// her istekte IP kontrolü yapılıyor (K-24, FR-22).
[Authorize(Policy = AccessPolicies.TakeExam)]
public sealed class SessionVerificationController(
    SessionVerificationService verification, SessionAccessService access, SessionIpGuard ipGuard) : ControllerBase
{
    private const int MaxVerifyBody = ImageFile.MaxFrames * ImageFile.MaxFrameBytes + 64 * 1024;

    // Kanıt karesi ve deneme sonucu adaya ayrıntı vermeden; skorlar yalnızca değerlendiriciye (A4).
    private const string RetryHint = "Yeniden deneyin; sorun sürerse destek talep edin.";

    [HttpPost("consent")]
    [RequestSizeLimit(1024)]
    [ProducesResponseType<SessionStateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Consent(Guid sessionId, SessionConsentRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();

        // Alan eksikse varsayılan false'a düşüp oturumu iptal etmesin; açıkça istenmeli.
        if (request.Accepted is not { } accepted)
        {
            return Problem(statusCode: 400, title: "Rızanın kabul edilip edilmediği belirtilmeli.");
        }

        if (!await access.IsCandidateOfAsync(sessionId, userId, ct))
        {
            return NotFound();
        }

        await ipGuard.CheckAsync(sessionId, HttpContext.Connection.RemoteIpAddress, ct);

        var status = await verification.RecordConsentAsync(sessionId, userId, accepted, request.TextVersion, ct);
        return status switch
        {
            SessionConsentStatus.Accepted => Ok(new SessionStateResponse(SessionState.AgentPairing)),
            SessionConsentStatus.Refused => Ok(new SessionStateResponse(SessionState.Cancelled)),
            SessionConsentStatus.SessionNotFound => NotFound(),
            SessionConsentStatus.ConsentVersionMismatch => Problem(statusCode: 400, title: "Rıza metni güncel değil; sayfayı yenileyip tekrar onaylayın."),
            SessionConsentStatus.NoReferencePhoto => Problem(statusCode: 409, title: "Kayıt fotoğrafı yüklenmeden sınava başlanamaz."),
            _ => Problem(statusCode: 409, title: "Oturum rıza adımında değil."),
        };
    }

    /// <summary>Kameradan alınan 3–5 kareyle canlılık testi ve yüz eşleştirmesi (docs/contracts.md 4. bölüm).</summary>
    [HttpPost("verify")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxVerifyBody)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxVerifyBody, MemoryBufferThreshold = MaxVerifyBody)]
    [ProducesResponseType<SessionStateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Verify(Guid sessionId, [FromForm] IFormFileCollection frames, CancellationToken ct)
    {
        var userId = User.GetUserId();

        if (frames.Count is < ImageFile.MinFrames or > ImageFile.MaxFrames)
        {
            return Problem(statusCode: 400, title: $"{ImageFile.MinFrames} ile {ImageFile.MaxFrames} arasında kare gönderilmeli.");
        }

        if (!await access.IsCandidateOfAsync(sessionId, userId, ct))
        {
            return NotFound();
        }

        await ipGuard.CheckAsync(sessionId, HttpContext.Connection.RemoteIpAddress, ct);

        var images = new List<byte[]>(frames.Count);
        foreach (var file in frames)
        {
            if (await FormFiles.ReadAsync(file, ImageFile.MaxFrameBytes, ct) is not { } bytes)
            {
                return Problem(statusCode: 400, title: "Kareler en fazla 1 MB olmalı.");
            }

            images.Add(bytes);
        }

        VerifyResult result;
        try
        {
            result = await verification.VerifyAsync(sessionId, userId, images, ct);
        }
        catch (FaceServiceUnavailableException)
        {
            return Problem(statusCode: 503, title: "Görüntü servisine şu an ulaşılamıyor.", detail: RetryHint);
        }

        return result.Status switch
        {
            VerifyStatus.Passed => Ok(new SessionStateResponse(SessionState.InProgress)),
            VerifyStatus.LivenessFailed => Problem(statusCode: 422, title: "Canlı yüz algılanamadı.", detail: RetryHint),
            VerifyStatus.IdentityMismatch => Problem(statusCode: 422, title: "Kimlik doğrulanamadı.", detail: RetryHint),
            VerifyStatus.SessionNotFound => NotFound(),
            VerifyStatus.InvalidFrames => Problem(statusCode: 400, title: "Kareler JPEG, PNG veya WebP olmalı."),
            VerifyStatus.TooSoon => Problem(statusCode: 429, title: "Yeni denemeden önce birkaç saniye bekleyin."),
            VerifyStatus.NoReferencePhoto => Problem(statusCode: 409, title: "Kayıt fotoğrafı bulunamadı.", detail: "Destek talep edin."),
            _ => Problem(statusCode: 409, title: "Oturum doğrulama adımında değil."),
        };
    }
}
