using AIGuard.Api.Auth;
using AIGuard.Application.Exams;
using AIGuard.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

public sealed record CreatedResponse(Guid Id);

/// <summary>Sınav, soru ve atama yönetimi (E3, FR-06–FR-08).</summary>
/// <remarks>
/// Değiştirme işlemleri yalnızca Admin. Listeleme ve ayrıntı her role açık; değerlendirici ve aday
/// yalnızca atandıkları sınavları görür. Doğru seçenek yalnızca yöneticiye giden soru listesinde var.
/// </remarks>
[ApiController]
[Route("api/exams")]
[Authorize]
public sealed class ExamsController(ExamService exams, AssignmentService assignments) : ControllerBase
{
    private const string AdminOnly = nameof(UserRole.Admin);

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ExamSummary>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ExamSummary>> List(CancellationToken ct) =>
        await exams.ListAsync(User.GetUserId(), User.GetUserRole(), ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ExamSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        await exams.GetAsync(id, User.GetUserId(), User.GetUserRole(), ct) is { } exam ? Ok(exam) : NotFound();

    [HttpPost]
    [Authorize(Roles = AdminOnly)]
    [RequestSizeLimit(4096)]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(ExamRequest request, CancellationToken ct) =>
        ToResponse(await exams.CreateAsync(request, ct), created: true);

    [HttpPut("{id:guid}")]
    [Authorize(Roles = AdminOnly)]
    [RequestSizeLimit(4096)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, ExamRequest request, CancellationToken ct) =>
        ToResponse(await exams.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        ToResponse(await exams.DeleteAsync(id, ct));

    /// <summary>Sorular, doğru seçenekle birlikte. Yalnızca Admin; adaya sorular E4'teki oturum ucundan gider.</summary>
    [HttpGet("{id:guid}/questions")]
    [Authorize(Roles = AdminOnly)]
    [ProducesResponseType<IReadOnlyList<QuestionDetail>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListQuestions(Guid id, CancellationToken ct) =>
        await exams.ListQuestionsAsync(id, ct) is { } questions ? Ok(questions) : NotFound();

    /// <summary>Çoktan seçmeli soru ekler; <c>order</c> verilmezse sona eklenir.</summary>
    [HttpPost("{id:guid}/questions")]
    [Authorize(Roles = AdminOnly)]
    [RequestSizeLimit(16384)] // en fazla 10 seçenek × 500 karakter + 2000 karakter metin
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddQuestion(Guid id, QuestionRequest request, CancellationToken ct) =>
        ToResponse(await exams.AddQuestionAsync(id, request, ct), created: true);

    /// <summary>Atanan aday ve değerlendiriciler (kartta yok; E6'daki atama ekranı için, K-23).</summary>
    [HttpGet("{id:guid}/assignments")]
    [Authorize(Roles = AdminOnly)]
    [ProducesResponseType<IReadOnlyList<AssignmentDetail>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListAssignments(Guid id, CancellationToken ct) =>
        await assignments.ListAsync(id, ct) is { } list ? Ok(list) : NotFound();

    /// <summary>Sınava aday (<c>Candidate</c>) veya değerlendirici (<c>Reviewer</c>) atar.</summary>
    [HttpPost("{id:guid}/assignments")]
    [Authorize(Roles = AdminOnly)]
    [RequestSizeLimit(1024)]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Assign(Guid id, AssignmentRequest request, CancellationToken ct) =>
        ToResponse(await assignments.AssignAsync(id, request, ct), created: true);

    private IActionResult ToResponse(ExamOperationResult result, bool created = false) => ExamResults.ToResponse(this, result, created);
}

/// <summary>Soru düzenleme ve silme (E3): <c>/api/questions/{id}</c>.</summary>
[ApiController]
[Route("api/questions")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class QuestionsController(ExamService exams) : ControllerBase
{
    /// <summary>Soruyu düzenler; sıralama da <c>order</c> alanıyla burada yapılır.</summary>
    [HttpPut("{id:guid}")]
    [RequestSizeLimit(16384)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, QuestionRequest request, CancellationToken ct) =>
        ExamResults.ToResponse(this, await exams.UpdateQuestionAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        ExamResults.ToResponse(this, await exams.DeleteQuestionAsync(id, ct));
}

/// <summary>Atama kaldırma (E3): <c>/api/assignments/{id}</c>.</summary>
[ApiController]
[Route("api/assignments")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AssignmentsController(AssignmentService assignments) : ControllerBase
{
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        ExamResults.ToResponse(this, await assignments.RemoveAsync(id, ct));
}

internal static class ExamResults
{
    /// <summary>Servis sonucunu HTTP yanıtına çevirir; Türkçe hata metni ProblemDetails'in detail alanında (K-20).</summary>
    public static IActionResult ToResponse(ControllerBase c, ExamOperationResult result, bool created = false) => result.Status switch
    {
        ExamOperationStatus.Success when created => c.StatusCode(StatusCodes.Status201Created, new CreatedResponse(result.Id!.Value)),
        ExamOperationStatus.Success => c.NoContent(),
        ExamOperationStatus.NotFound => c.NotFound(),
        ExamOperationStatus.Conflict => c.Problem(statusCode: StatusCodes.Status409Conflict, title: "Çakışma.", detail: result.Error),
        _ => c.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Geçersiz istek.", detail: result.Error),
    };
}
