using AIGuard.Api.Auth;
using AIGuard.Application.Risk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

/// <summary>
/// Sınava özel risk ayarları (K-49, Stitch 29). Platform varsayılanı <c>GET /api/risk-settings</c>; burada sınavın
/// etkin değerleri ve hangilerinin değiştirildiği. Sınavda oturum varsa değiştirilemez (409).
/// </summary>
[ApiController]
[Route("api/exams/{examId:guid}/risk-settings")]
[Authorize(Policy = AccessPolicies.Manage)]
public sealed class ExamRiskSettingsController(ExamRiskService risks) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ExamRiskSettingsView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid examId, CancellationToken ct) =>
        await risks.GetAsync(examId, ct) is { } view ? Ok(view) : NotFound();

    /// <summary>Oturum eşiği ve yalnızca varsayılandan farklı olay türleri; listede olmayan tür varsayılana döner.</summary>
    [HttpPut]
    [RequestSizeLimit(16 * 1024)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid examId, ExamRiskSettingsRequest request, CancellationToken ct) =>
        ToResult(await risks.UpdateAsync(User.GetUserId(), examId, request, ct));

    /// <summary>Bütün sınav ayarlarını platform varsayılanına döndürür.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reset(Guid examId, CancellationToken ct) =>
        ToResult(await risks.UpdateAsync(User.GetUserId(), examId, new ExamRiskSettingsRequest(null, []), ct));

    private IActionResult ToResult(ExamRiskResult result) => result.Status switch
    {
        ExamRiskStatus.Success => NoContent(),
        ExamRiskStatus.NotFound => NotFound(),
        ExamRiskStatus.Locked => Problem(statusCode: StatusCodes.Status409Conflict, title: "Çakışma.", detail: result.Error),
        _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Geçersiz istek.", detail: result.Error),
    };
}
