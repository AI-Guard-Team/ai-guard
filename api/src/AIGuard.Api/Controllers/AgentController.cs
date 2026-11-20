using AIGuard.Application.Agents;
using AIGuard.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

/// <param name="Code">Tarayıcının ajana verdiği tek kullanımlık kod.</param>
/// <param name="Os">Ajanın çalıştığı işletim sistemi: "Windows" veya "MacOS".</param>
public sealed record AgentPairRequest(string? Code, AgentPlatform? Os);

/// <param name="SessionKey">Y2: sinyalleri imzalayacak 32 baytlık anahtar, Base64. Yalnızca bu yanıtta, TLS üzerinden veriliyor.</param>
public sealed record AgentPairResponse(Guid PairingId, Guid SessionId, string SessionKey);

/// <summary>Güvenlik ajanının çağırdığı uçlar (Y1, Y2).</summary>
/// <remarks>
/// Bu uçlar JWT ile değil korunuyor: ajanın kullanıcı oturumu yok. Eşleştirmede kanıt tek kullanımlık
/// kod (kodu yalnızca oturumun sahibi aday alabiliyor, E2); sinyalde kanıt oturum anahtarıyla
/// atılmış HMAC imzası (K-21).
/// </remarks>
[ApiController]
[Route("api/agent")]
[AllowAnonymous] // varsayılan "giriş gerekli" kuralının dışında; kanıt eşleştirme kodu ve HMAC imzası (K-20, K-21)
public sealed class AgentController(AgentPairingService pairing, AgentSignalService signals) : ControllerBase
{
    [HttpPost("pair")]
    [RequestSizeLimit(1024)] // gövde iki kısa alan
    [ProducesResponseType<AgentPairResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Pair(AgentPairRequest request, CancellationToken ct)
    {
        var result = await pairing.PairAsync(request.Code, request.Os, ct);

        // Anahtar bir kimlik bilgisi: ara bellek ya da vekil sunucu yanıtı saklamasın.
        Response.Headers.CacheControl = "no-store";

        // Red nedeni (yok / süresi dolmuş / kullanılmış) yanıtta ayrılmıyor.
        return result.Status == PairAgentStatus.Paired
            ? Ok(new AgentPairResponse(result.PairingId!.Value, result.SessionId!.Value, Convert.ToBase64String(result.SessionKey!)))
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: "Eşleştirme kodu geçersiz veya süresi dolmuş.");
    }

    /// <summary>İmzalı ajan sinyali (Y2): <c>{ sessionId, ts, nonce, indicators[], sig }</c>.</summary>
    /// <remarks>
    /// 202: kabul · 400: bozuk gövde · 401: imza/zaman/tekrar ya da anahtar yok · 404: eşleşme yok ·
    /// 410: oturum bitti, ajan göndermeyi bırakmalı. Red nedeni ajana ayrılmıyor; olay ayrıntısında.
    /// </remarks>
    [HttpPost("signal")]
    [RequestSizeLimit(4096)] // en fazla 32 kısa gösterge
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Signal(AgentSignalRequest request, CancellationToken ct)
    {
        var result = await signals.ProcessAsync(request, ct);

        return result.Status switch
        {
            AgentSignalStatus.Accepted => Accepted(),
            AgentSignalStatus.Malformed => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Geçersiz sinyal."),
            AgentSignalStatus.NotFound => NotFound(),
            AgentSignalStatus.SessionEnded => Problem(statusCode: StatusCodes.Status410Gone, title: "Oturum bitti."),
            _ => Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sinyal doğrulanamadı."),
        };
    }
}
