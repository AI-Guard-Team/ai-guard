using AIGuard.Api.Auth;
using AIGuard.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

/// <summary>Kabuktaki bildirim zili (K-49). Rolüne göre; her istekte güncel veriden türetiliyor.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(NotificationService notifications) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotificationItem>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<NotificationItem>> List(CancellationToken ct)
    {
        // Kişisel veri (aday adı, sınav) içeriyor.
        Response.Headers.CacheControl = "no-store";
        return await notifications.ListAsync(User.GetUserId(), User.GetUserRole(), ct);
    }
}
