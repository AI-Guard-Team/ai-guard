using AIGuard.Api.Auth;
using AIGuard.Application.Review;
using AIGuard.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

/// <summary>
/// Değerlendirici paneli (A4, FR-27–FR-33). Okuma uçları atanmış değerlendirici ve Admin'e, karar, not ve
/// yanlış alarm yalnızca atanmış değerlendiriciye açık (K-31). Atanmadığı sınavın oturumu 404.
/// </summary>
[ApiController]
[Authorize(Roles = $"{nameof(UserRole.Reviewer)},{nameof(UserRole.Admin)}")]
public sealed class ReviewController(ReviewService review) : ControllerBase
{
    // 4000 karakterlik not JSON'da kaçışlarla (ör. ç) birkaç katına çıkabiliyor; 32 KB bunu karşılıyor,
    // varsayılan 30 MB'lık sınırı ise kapatıyor.
    private const int MaxBodyBytes = 32 * 1024;

    /// <summary>
    /// Sınavın oturumları: tamamlananlar, karar verilenler ve hiç başlamayanlar. Varsayılan sırada karar
    /// bekleyenler ve işaretliler önde. <c>pendingCount</c> karar bekleyen oturum sayısı (FR-29).
    /// </summary>
    /// <param name="examId">Sınav.</param>
    /// <param name="risky">true: yalnızca riskli; false: yalnızca riskli olmayan.</param>
    /// <param name="flags">Bu işaretlerden en az biri olanlar (ör. <c>flags=NoAgent&amp;flags=Indicated</c>).</param>
    /// <param name="sort">Priority (varsayılan), Score ya da StartedAt.</param>
    /// <param name="ct">İptal.</param>
    [HttpGet("api/review/exams/{examId:guid}/sessions")]
    [ProducesResponseType<ReviewSessionList>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid examId, [FromQuery] bool? risky, [FromQuery] SessionFlags[]? flags,
        [FromQuery] ReviewSort sort = ReviewSort.Priority, CancellationToken ct = default)
    {
        // Aday adları içeriyor; ara önbelleklerde kalmasın.
        Response.Headers.CacheControl = "no-store";
        var list = await review.ListAsync(examId, Actor(), new ReviewListQuery(risky, flags, sort), ct);
        return list is null ? NotFound() : Ok(list);
    }

    /// <summary>
    /// Oturum detayı: olay zaman çizelgesi, sınav bitişindeki katkılar, skor geçmişi (grafik verisi), notlar,
    /// karar ve kanıt karesi bağlantıları. Bağlantılar 5 dakika geçerli; süresi dolunca detay yeniden istenmeli.
    /// </summary>
    [HttpGet("api/review/sessions/{id:guid}")]
    [ProducesResponseType<ReviewSessionDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        // Yanıtta imzalı bağlantılar var; ara önbelleklerde kalmasın.
        Response.Headers.CacheControl = "no-store";
        return await review.GetAsync(id, Actor(), ct) is { } detail ? Ok(detail) : NotFound();
    }

    /// <summary>
    /// Karar: <c>{ decision: Clean | Suspicious | Violation, note }</c>. Yalnızca riskli veya işaretli olarak
    /// tamamlanmış oturumda ve bir kez; oturum Decided olur, karar denetim kaydına yazılır.
    /// </summary>
    [HttpPost("api/review/sessions/{id:guid}/decision")]
    [Authorize(Roles = nameof(UserRole.Reviewer))]
    [RequestSizeLimit(MaxBodyBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Decide(Guid id, DecisionRequest request, CancellationToken ct) =>
        ToResponse(await review.DecideAsync(id, Actor(), request, ct));

    /// <summary>Oturuma not ekler: <c>{ text }</c>. Karardan önce ya da sonra yazılabilir; notlar düzenlenmez.</summary>
    [HttpPost("api/review/sessions/{id:guid}/notes")]
    [Authorize(Roles = nameof(UserRole.Reviewer))]
    [RequestSizeLimit(MaxBodyBytes)]
    [ProducesResponseType<ReviewNoteView>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddNote(Guid id, NoteRequest request, CancellationToken ct)
    {
        var (result, note) = await review.AddNoteAsync(id, Actor(), request, ct);
        return note is null ? ToResponse(result) : StatusCode(StatusCodes.Status201Created, note);
    }

    /// <summary>
    /// Olayı yanlış alarm işaretler. Olay silinmez; işaretleyen ve zaman kaydedilir, olay skordan çıkar.
    /// Karar verilmiş oturumda 409.
    /// </summary>
    [HttpPost("api/events/{id:guid}/false-alarm")]
    [Authorize(Roles = nameof(UserRole.Reviewer))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkFalseAlarm(Guid id, CancellationToken ct) =>
        ToResponse(await review.MarkFalseAlarmAsync(id, Actor(), ct));

    private ReviewActor Actor() => new(User.GetUserId(), User.GetUserRole());

    private IActionResult ToResponse(ReviewOperationResult result) => result.Status switch
    {
        ReviewOperationStatus.Success => NoContent(),
        ReviewOperationStatus.NotFound => NotFound(),
        ReviewOperationStatus.Invalid => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Geçersiz istek.", detail: result.Error),
        _ => Problem(statusCode: StatusCodes.Status409Conflict, title: "İşlem yapılamıyor.", detail: result.Error),
    };
}
