using AIGuard.Api.Auth;
using AIGuard.Application.Agents;
using AIGuard.Application.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

public sealed record PairingCodeResponse(string Code, DateTimeOffset ExpiresAt);

/// <summary>Tarayıcının ajana göndereceği tek kullanımlık kodu üretir (Y1, UC-08 adım 1).</summary>
/// <remarks>
/// Kod adaya gösterilmemeli; tarayıcı yanıtı alıp doğrudan 127.0.0.1:47321/pair adresine gönderir.
/// </remarks>
[ApiController]
[Route("api/sessions/{sessionId:guid}/agent-code")]
// Kart: yalnızca Candidate ve yalnızca kendi oturumu (E2, S-12).
[Authorize(Policy = AccessPolicies.TakeExam)]
public sealed class SessionAgentPairingController(AgentPairingService pairing, SessionAccessService access) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<PairingCodeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Post(Guid sessionId, CancellationToken ct)
    {
        // Başka adayın oturumuna kod alınıp kendi ajanını o oturuma bağlayamasın.
        if (!await access.IsCandidateOfAsync(sessionId, User.GetUserId(), ct))
        {
            return NotFound();
        }

        var result = await pairing.IssueCodeAsync(sessionId, ct);

        // Kod bir kimlik bilgisi: tarayıcı veya ara sunucu önbelleğine yazılmasın.
        Response.Headers.CacheControl = "no-store";

        return result.Status switch
        {
            IssuePairingCodeStatus.Issued =>
                StatusCode(StatusCodes.Status201Created, new PairingCodeResponse(result.Code!, result.ExpiresAt!.Value)),
            IssuePairingCodeStatus.SessionNotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status409Conflict, title: "Oturum ajan eşleştirme aşamasında değil."),
        };
    }
}
