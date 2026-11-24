using AIGuard.Api.Auth;
using AIGuard.Application.Events;
using AIGuard.Application.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

/// <summary>Tarayıcıdan gelen sınav olayları (A2, S4).</summary>
/// <remarks>Yalnızca aday ve yalnızca kendi oturumuna yazabilir (E2, S-9).</remarks>
[ApiController]
[Route("api/sessions/{sessionId:guid}/events")]
[Authorize(Policy = AccessPolicies.TakeExam)]
public sealed class SessionEventsController(EventService events, SessionIpGuard ipGuard, SessionAccessService access) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(2048)] // gövde birkaç alan; büyük gövdeyle sunucuyu yorma girişimine karşı
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Post(Guid sessionId, BrowserEventRequest request, CancellationToken ct)
    {
        // Başkasının oturumu "yok" gibi yanıtlanıyor; oturum kimliğinin var olup olmadığı sızmasın.
        if (!await access.IsCandidateOfAsync(sessionId, User.GetUserId(), ct))
        {
            return NotFound();
        }

        await ipGuard.CheckAsync(sessionId, HttpContext.Connection.RemoteIpAddress, ct);

        var result = await events.RecordBrowserEventAsync(sessionId, request, ct);

        // Yanıtta skor ve katkı dönmüyor: aday, hangi davranışın ne kadar puan getirdiğini görmemeli.
        return result.Status switch
        {
            EventRecordStatus.Recorded => StatusCode(StatusCodes.Status201Created, new { id = result.Event!.Id }),
            EventRecordStatus.SessionNotFound => NotFound(),
            EventRecordStatus.SessionClosed => Problem(statusCode: StatusCodes.Status409Conflict, title: "Oturum kapalı."),
            EventRecordStatus.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error ?? "Geçersiz olay."),
        };
    }
}
