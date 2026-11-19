using AIGuard.Api.Filters;
using AIGuard.Application.Events;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

/// <summary>Tarayıcıdan gelen sınav olayları (A2, S4).</summary>
[ApiController]
[Route("api/sessions/{sessionId:guid}/events")]
[DevelopmentOnly] // E2 gelene kadar; ayrıntı DevelopmentOnlyAttribute'ta.
public sealed class SessionEventsController(EventService events, SessionIpGuard ipGuard) : ControllerBase
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
