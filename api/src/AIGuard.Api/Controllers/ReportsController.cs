using AIGuard.Api.Auth;
using AIGuard.Application.Review;
using AIGuard.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

/// <summary>Aday bazında sınav raporu (A4; E6'daki rapor sayfası). Erişim inceleme uçlarıyla aynı (K-31).</summary>
[ApiController]
[Authorize(Roles = $"{nameof(UserRole.Reviewer)},{nameof(UserRole.Admin)}")]
public sealed class ReportsController(ReportService reports) : ControllerBase
{
    /// <summary>
    /// Sınav sonucu (doğru/yanlış/boş, izlenmeyen sürede verilen cevaplar), risk skoru ve işaretler, olay türü
    /// özeti, en önemli gerekçeler, doğrulama denemesi sayısı, karar ve notlar. Hiç başlamayan oturumda sonuç boş.
    /// </summary>
    [HttpGet("api/reports/exams/{examId:guid}/sessions/{sessionId:guid}")]
    [ProducesResponseType<CandidateReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid examId, Guid sessionId, CancellationToken ct)
    {
        // Kişisel veri ve sınav sonucu içeriyor.
        Response.Headers.CacheControl = "no-store";
        var report = await reports.GetAsync(examId, sessionId, new ReviewActor(User.GetUserId(), User.GetUserRole()), ct);
        return report is null ? NotFound() : Ok(report);
    }
}
