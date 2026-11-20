using AIGuard.Api.Auth;
using AIGuard.Application.Agents;
using AIGuard.Application.Events;
using AIGuard.Application.Sessions;
using AIGuard.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

public sealed record AgentContinueResponse(SessionState State);

/// <summary>
/// Ajan eşleştirme adımındaki aday uçları (Y4, UC-08 4a/4b): ajan durumu ve göstergeler, ajansız ya da
/// göstergeli devam. Yalnızca aday ve yalnızca kendi oturumu (K-20); her istekte IP kontrolü (FR-22).
/// </summary>
[ApiController]
[Route("api/sessions/{sessionId:guid}/agent")]
[Authorize(Roles = nameof(UserRole.Candidate))]
public sealed class SessionAgentController(
    AgentContinueService agent, SessionAccessService access, SessionIpGuard ipGuard) : ControllerBase
{
    /// <summary>
    /// Ajanın durumu ve bulduğu göstergeler; her göstergede adaya gösterilecek neden ve çözüm adımı var.
    /// Önyüz eşleştirmeden sonra birkaç saniyede bir çağırıp aday göstergeyi giderince devam edebilir.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<AgentStatusView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid sessionId, CancellationToken ct)
    {
        if (!await access.IsCandidateOfAsync(sessionId, User.GetUserId(), ct))
        {
            return NotFound();
        }

        await ipGuard.CheckAsync(sessionId, HttpContext.Connection.RemoteIpAddress, ct);
        return await agent.GetStatusAsync(sessionId, ct) is { } status ? Ok(status) : NotFound();
    }

    /// <summary>
    /// Gerekçeyle devam: <c>{ mode: "noAgent" | "indicated", reason }</c>. Gerekçe oturuma yazılır, işlem
    /// denetim kaydına girer, oturum <c>IdentityVerification</c> durumuna geçer.
    /// </summary>
    /// <remarks>
    /// 200: devam edildi · 400: geçersiz mod ya da gerekçe · 404 · 409: oturum bu adımda değil, ajansız
    /// devam istendi ama ajan çalışıyor ya da göstergeli devam istendi ama gösterge yok.
    /// </remarks>
    [HttpPost("continue")]
    [RequestSizeLimit(8192)] // gerekçe en fazla 2000 karakter
    [ProducesResponseType<AgentContinueResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Continue(Guid sessionId, AgentContinueRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!await access.IsCandidateOfAsync(sessionId, userId, ct))
        {
            return NotFound();
        }

        await ipGuard.CheckAsync(sessionId, HttpContext.Connection.RemoteIpAddress, ct);
        var result = await agent.ContinueAsync(sessionId, userId, request, ct);

        return result.Status switch
        {
            AgentContinueStatus.Continued => Ok(new AgentContinueResponse(SessionState.IdentityVerification)),
            AgentContinueStatus.NotFound => NotFound(),
            AgentContinueStatus.Invalid => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Geçersiz istek.", detail: result.Error),
            _ => Problem(statusCode: StatusCodes.Status409Conflict, title: "Devam edilemiyor.", detail: result.Error),
        };
    }
}
