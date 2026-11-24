using AIGuard.Application.ConsentTexts;
using AIGuard.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

/// <summary>
/// KVKK aydınlatma ve rıza metinleri (A5, K-32). Önyüz rıza ekranında güncel metni gösterip <c>version</c>'ı rıza
/// isteğiyle (<c>POST /api/me/photo</c>, <c>POST /api/sessions/{id}/consent</c>) geri gönderiyor.
/// </summary>
/// <remarks>Metinler herkese aynı ve kişisel veri içermiyor; giriş yapmış her kullanıcı okuyabilir (K-20 varsayılanı).</remarks>
[ApiController]
[Route("api/consent-texts")]
public sealed class ConsentTextsController(ConsentTextService texts) : ControllerBase
{
    /// <summary>Türün güncel metni: <c>Biometric</c> (kayıt fotoğrafı) ya da <c>Session</c> (sınav izleme).</summary>
    [HttpGet("{type}")]
    [ProducesResponseType<ConsentTextView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrent(ConsentType type, CancellationToken ct) =>
        await texts.GetCurrentAsync(type, ct) is { } text ? Ok(text) : NotFound();

    /// <summary>Belirli bir sürüm; adayın geçmişte kabul ettiği metni göstermek için.</summary>
    [HttpGet("{type}/{version}")]
    [ProducesResponseType<ConsentTextView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(ConsentType type, string version, CancellationToken ct) =>
        await texts.GetAsync(type, version, ct) is { } text ? Ok(text) : NotFound();
}
