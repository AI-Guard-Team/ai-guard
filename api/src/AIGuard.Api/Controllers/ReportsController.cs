using AIGuard.Api.Auth;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Review;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

/// <summary>Aday bazında sınav raporu (A4; E6'daki rapor sayfası). Erişim inceleme uçlarıyla aynı (K-31).</summary>
[ApiController]
[Authorize(Policy = AccessPolicies.ReviewRead)]
public sealed class ReportsController(ReportService reports, IEnumerable<IReportExporter> exporters, TimeProvider clock)
    : ControllerBase
{
    /// <summary>
    /// Sınav sonucu (doğru/yanlış/boş, izlenmeyen sürede verilen cevaplar), risk skoru ve işaretler, olay türü
    /// özeti, en önemli gerekçeler, doğrulama denemesi sayısı, karar ve notlar. Hiç başlamayan oturumda sonuç boş.
    /// </summary>
    /// <param name="examId">Sınav.</param>
    /// <param name="sessionId">Oturum.</param>
    /// <param name="format">Boşsa JSON; <c>Xlsx</c> ya da <c>Pdf</c> verilirse aynı rapor dosya olarak iner (FR-34).</param>
    /// <param name="ct">İptal.</param>
    [HttpGet("api/reports/exams/{examId:guid}/sessions/{sessionId:guid}")]
    [ProducesResponseType<CandidateReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid examId, Guid sessionId, [FromQuery] ReportFormat? format, CancellationToken ct)
    {
        // Kişisel veri ve sınav sonucu içeriyor.
        Response.Headers.CacheControl = "no-store";
        var report = await reports.GetAsync(examId, sessionId, new ReviewActor(User.GetUserId(), User.GetUserRole()), format, ct);
        if (report is null)
        {
            return NotFound();
        }

        if (format is not { } requested)
        {
            return Ok(report);
        }

        var file = exporters.Single(e => e.Format == requested).Export(report, clock.GetUtcNow());
        return File(file.Content, file.ContentType, file.FileName);
    }
}
