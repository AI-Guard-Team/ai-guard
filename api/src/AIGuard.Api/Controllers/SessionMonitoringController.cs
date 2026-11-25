using AIGuard.Api.Auth;
using AIGuard.Application.Events;
using AIGuard.Application.Sessions;
using AIGuard.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

/// <param name="Granted">Zorunlu. false: kamera/mikrofon izni kaldırıldı; true: izin yeniden verildi.</param>
public sealed record PermissionRequest(bool? Granted);

/// <summary>
/// Sınav sırasında tarayıcıdan gelen izleme verisi (S4, FR-14, FR-17): saniyede bir kamera karesi, ~1 sn'lik ses
/// parçaları ve kamera/mikrofon izni. Yalnızca aday ve yalnızca kendi oturumu; başkasının oturumu 404 (K-20).
/// </summary>
/// <remarks>
/// Yanıtlarda analiz sonucu yok: aday hangi karenin şüpheli bulunduğunu görmemeli. Kare ve ses diske yazılmıyor.
/// </remarks>
[ApiController]
[Route("api/sessions/{sessionId:guid}")]
[Authorize(Policy = AccessPolicies.TakeExam)]
public sealed class SessionMonitoringController(
    SessionMediaService media, SessionService sessions, SessionAccessService access, SessionIpGuard ipGuard) : ControllerBase
{
    // Çok parçalı gövdenin sınır, alan adı ve dosya adı için payı.
    private const int FormOverhead = 8 * 1024;
    private const int MaxFrameBody = SessionMediaService.MaxFrameBytes + FormOverhead;
    private const int MaxAudioBody = SessionMediaService.MaxAudioBytes + FormOverhead;

    /// <summary>Kamera karesi: multipart, alan adı <c>frame</c>, JPEG/PNG/WebP, en fazla 1 MB. Kabul edilirse 202.</summary>
    [HttpPost("frames")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFrameBody)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFrameBody, MemoryBufferThreshold = MaxFrameBody)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Frame(Guid sessionId, IFormFile? frame, CancellationToken ct)
    {
        if (await AuthorizeAsync(sessionId, ct) is { } denied)
        {
            return denied;
        }

        if (await FormFiles.ReadAsync(frame, SessionMediaService.MaxFrameBytes, ct) is not { } bytes)
        {
            return Problem(statusCode: 400, title: "Kare en fazla 1 MB olmalı.");
        }

        return ToResponse(await media.ReceiveFrameAsync(sessionId, bytes, ct), "Kare JPEG, PNG veya WebP olmalı.");
    }

    /// <summary>Ses parçası: multipart, alan adı <c>audio</c>, WebM (Opus) ya da WAV, en fazla 256 KB. Kabul edilirse 202.</summary>
    [HttpPost("audio")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxAudioBody)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxAudioBody, MemoryBufferThreshold = MaxAudioBody)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Audio(Guid sessionId, IFormFile? audio, CancellationToken ct)
    {
        if (await AuthorizeAsync(sessionId, ct) is { } denied)
        {
            return denied;
        }

        if (await FormFiles.ReadAsync(audio, SessionMediaService.MaxAudioBytes, ct) is not { } bytes)
        {
            return Problem(statusCode: 400, title: "Ses parçası en fazla 256 KB olmalı.");
        }

        return ToResponse(await media.ReceiveAudioAsync(sessionId, bytes, ct), "Ses parçası WebM veya WAV olmalı.");
    }

    /// <summary>
    /// Kamera/mikrofon izni: <c>{ "granted": false }</c> sınavı PermissionPending'e alır (sorular gizlenir, süre işler),
    /// <c>{ "granted": true }</c> sınava döndürür. İzinsiz süre sunucuda ölçülüp PERMISSION_REVOKED olarak yazılır (K-30).
    /// Aynı bildirim tekrar gelirse 200 ve durum değişmez.
    /// </summary>
    [HttpPost("permission")]
    [RequestSizeLimit(256)]
    [ProducesResponseType<SessionStateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Permission(Guid sessionId, PermissionRequest request, CancellationToken ct)
    {
        // Alan eksikse "izin yok" varsayılıp sınav durdurulmasın; açıkça belirtilmeli.
        if (request.Granted is not { } granted)
        {
            return Problem(statusCode: 400, title: "İznin verilip verilmediği belirtilmeli.");
        }

        if (await AuthorizeAsync(sessionId, ct) is { } denied)
        {
            return denied;
        }

        var result = granted
            ? await sessions.RestorePermissionAsync(sessionId, ct)
            : await sessions.RevokePermissionAsync(sessionId, ct);
        var state = granted ? SessionState.InProgress : SessionState.PermissionPending;
        return result.Status switch
        {
            SessionOperationStatus.Success or SessionOperationStatus.Duplicate => Ok(new SessionStateResponse(state)),
            SessionOperationStatus.NotFound => NotFound(),
            _ => Problem(statusCode: 409, title: "İşlem yapılamıyor.", detail: result.Error),
        };
    }

    private async Task<IActionResult?> AuthorizeAsync(Guid sessionId, CancellationToken ct)
    {
        if (!await access.IsCandidateOfAsync(sessionId, User.GetUserId(), ct))
        {
            return NotFound();
        }

        await ipGuard.CheckAsync(sessionId, HttpContext.Connection.RemoteIpAddress, ct);
        return null;
    }

    private IActionResult ToResponse(MediaReceiveStatus status, string invalidTitle) => status switch
    {
        MediaReceiveStatus.Accepted => StatusCode(StatusCodes.Status202Accepted),
        MediaReceiveStatus.Invalid => Problem(statusCode: 400, title: invalidTitle),
        MediaReceiveStatus.NotFound => NotFound(),
        MediaReceiveStatus.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests),
        _ => Problem(statusCode: 409, title: "Sınav sürmüyor; izleme verisi kabul edilmedi."),
    };
}
