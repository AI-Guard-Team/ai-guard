using AIGuard.Application.Agents;
using AIGuard.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

/// <param name="Code">Tarayıcının ajana verdiği tek kullanımlık kod.</param>
/// <param name="Os">Ajanın çalıştığı işletim sistemi: "Windows" veya "MacOS".</param>
public sealed record AgentPairRequest(string? Code, AgentPlatform? Os);

public sealed record AgentPairResponse(Guid PairingId, Guid SessionId);

/// <summary>Güvenlik ajanının çağırdığı uçlar (Y1).</summary>
/// <remarks>
/// Bu uç JWT ile değil eşleştirme koduyla korunuyor (kart Y1): ajanın kullanıcı oturumu yok,
/// kimlik kanıtı tek kullanımlık kodun kendisi. Kod yalnızca Development'ta açık olan uçtan
/// üretildiği için (E2'ye kadar) Production'da eşleşme oluşamıyor.
/// </remarks>
[ApiController]
[Route("api/agent")]
public sealed class AgentController(AgentPairingService pairing) : ControllerBase
{
    [HttpPost("pair")]
    [RequestSizeLimit(1024)] // gövde iki kısa alan
    [ProducesResponseType<AgentPairResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Pair(AgentPairRequest request, CancellationToken ct)
    {
        var result = await pairing.PairAsync(request.Code, request.Os, ct);

        // Red nedeni (yok / süresi dolmuş / kullanılmış) yanıtta ayrılmıyor.
        return result.Status == PairAgentStatus.Paired
            ? Ok(new AgentPairResponse(result.PairingId!.Value, result.SessionId!.Value))
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: "Eşleştirme kodu geçersiz veya süresi dolmuş.");
    }
}
