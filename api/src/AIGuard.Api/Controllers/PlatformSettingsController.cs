using AIGuard.Api.Auth;
using AIGuard.Application.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

/// <summary>
/// Platform ayarları (K-49): alternatif sınav yöntemi bilgisi, destek iletişimi, güvenlik ajanı indirme adresleri.
/// Giriş yapmış herkes okur (aday rızayı reddedince ya da ajanı indirirken görüyor); yalnızca yönetici değiştirir.
/// </summary>
[ApiController]
[Route("api/platform-settings")]
public sealed class PlatformSettingsController(PlatformSettingsService settings) : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<PlatformSettingsView>(StatusCodes.Status200OK)]
    public Task<PlatformSettingsView> Get(CancellationToken ct) => settings.GetAsync(ct);

    [HttpPut]
    [Authorize(Policy = AccessPolicies.Manage)]
    [RequestSizeLimit(16 * 1024)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(PlatformSettingsRequest request, CancellationToken ct)
    {
        var result = await settings.UpdateAsync(User.GetUserId(), request, ct);
        return result.Success
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: "Geçersiz istek.", detail: result.Error);
    }
}
