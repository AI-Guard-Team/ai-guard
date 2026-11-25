using AIGuard.Api.Auth;
using AIGuard.Application.Risk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AIGuard.Api.Controllers;

/// <summary>
/// Risk ayarları (UI1, Stitch 29; K-40). Salt okunur: değerler riskconfig.json'dan, değişiklik sunucu yapılandırmasıyla.
/// Sınav başına düzenleme ayrı bir karar (S-27).
/// </summary>
[ApiController]
[Route("api/risk-settings")]
[Authorize(Policy = AccessPolicies.Manage)]
public sealed class RiskSettingsController(IOptions<RiskOptions> options) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<RiskSettingsView>(StatusCodes.Status200OK)]
    public RiskSettingsView Get() => RiskSettingsView.From(options.Value);
}
