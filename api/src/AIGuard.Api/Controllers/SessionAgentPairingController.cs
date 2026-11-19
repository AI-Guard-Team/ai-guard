using AIGuard.Api.Filters;
using AIGuard.Application.Agents;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

public sealed record PairingCodeResponse(string Code, DateTimeOffset ExpiresAt);

/// <summary>Tarayıcının ajana göndereceği tek kullanımlık kodu üretir (Y1).</summary>
/// <remarks>
/// Kod adaya gösterilmemeli; tarayıcı yanıtı alıp doğrudan 127.0.0.1:47321/pair adresine gönderir.
/// </remarks>
[ApiController]
[Route("api/sessions/{sessionId:guid}/agent/pairing-code")]
[DevelopmentOnly] // E2 gelene kadar: "oturum bu adaya mı ait" kontrolü olmadan dışarı açılmamalı (S-9 ile aynı).
public sealed class SessionAgentPairingController(AgentPairingService pairing) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<PairingCodeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Post(Guid sessionId, CancellationToken ct)
    {
        var result = await pairing.IssueCodeAsync(sessionId, ct);

        // Kod bir kimlik bilgisi: tarayıcı veya ara sunucu önbelleğine yazılmasın.
        Response.Headers.CacheControl = "no-store";

        return result.Status switch
        {
            IssuePairingCodeStatus.Issued =>
                StatusCode(StatusCodes.Status201Created, new PairingCodeResponse(result.Code!, result.ExpiresAt!.Value)),
            IssuePairingCodeStatus.SessionNotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status409Conflict, title: "Oturum kapalı."),
        };
    }
}
