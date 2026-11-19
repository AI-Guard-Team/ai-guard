using AIGuard.Application.Agents;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

public sealed record AgentPairRequest(string? Code);

public sealed record AgentPairResponse(Guid PairingId, Guid SessionId);

/// <summary>Güvenlik ajanının çağırdığı uçlar (Y1).</summary>
/// <remarks>
/// Ajanın kullanıcı oturumu yok; buradaki kimlik kanıtı tek kullanımlık eşleştirme kodunun
/// kendisi. Kod yalnızca Development'ta açık olan uçtan üretildiği için (E2'ye kadar)
/// Production'da eşleşme oluşamıyor.
/// </remarks>
[ApiController]
[Route("api/agent")]
public sealed class AgentController(AgentPairingService pairing) : ControllerBase
{
    [HttpPost("pair")]
    [RequestSizeLimit(1024)] // gövde tek bir 43 karakterlik alan
    [ProducesResponseType<AgentPairResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Pair(AgentPairRequest request, CancellationToken ct)
    {
        var result = await pairing.PairAsync(request.Code, ct);

        // Red nedeni (yok / süresi dolmuş / kullanılmış) yanıtta ayrılmıyor.
        return result.Status == PairAgentStatus.Paired
            ? Ok(new AgentPairResponse(result.PairingId!.Value, result.SessionId!.Value))
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: "Eşleştirme kodu geçersiz veya süresi dolmuş.");
    }
}
