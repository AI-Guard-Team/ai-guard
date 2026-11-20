using AIGuard.Application.Abstractions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Exams;

/// <summary>
/// Sınav ve soru yönetimi (E3, FR-06, FR-07). Değiştirme işlemleri yalnızca yöneticiye açık (uçta);
/// listeleme rol bazlı: değerlendirici ve aday yalnızca atandığı sınavları görür (FR-08).
/// </summary>
/// <remarks>
/// Sınavda bir oturum başladıktan sonra süre, pencere ve sorular değiştirilemiyor: adayların bir kısmı
/// eski sorularla, bir kısmı yenileriyle sınava girmiş olurdu ve verilen cevaplar anlamını yitirirdi (K-23).
/// </remarks>
public sealed class ExamService(IAppDbContext db, TimeProvider clock)
{
    private const string LockedMessage = "Bu sınavda oturum başlamış; sınav ve sorular artık değiştirilemez.";

    public async Task<IReadOnlyList<ExamSummary>> ListAsync(Guid userId, UserRole role, CancellationToken ct = default)
    {
        var query = db.Exams.AsNoTracking();
        if (role != UserRole.Admin)
        {
            var assignmentRole = role == UserRole.Reviewer ? AssignmentRole.Reviewer : AssignmentRole.Candidate;
            query = query.Where(e => e.Assignments.Any(a => a.UserId == userId && a.Role == assignmentRole));
        }

        var rows = await query
            .OrderBy(e => e.WindowStartAt)
            .Select(e => new
            {
                Exam = e,
                QuestionCount = e.Questions.Count,
                AttemptUsedAt = e.Assignments.Where(a => a.UserId == userId).Select(a => a.AttemptUsedAt).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return rows.Select(r => ToSummary(r.Exam, r.QuestionCount, role, r.AttemptUsedAt)).ToList();
    }

    /// <summary>Yönetici her sınavı, diğer roller yalnızca atandıkları sınavı görür; görmüyorsa null.</summary>
    public async Task<ExamSummary?> GetAsync(Guid examId, Guid userId, UserRole role, CancellationToken ct = default)
    {
        var exam = await db.Exams.AsNoTracking().SingleOrDefaultAsync(e => e.Id == examId, ct);
        if (exam is null)
        {
            return null;
        }

        var assignment = await db.ExamAssignments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.ExamId == examId && a.UserId == userId, ct);
        var expected = role == UserRole.Reviewer ? AssignmentRole.Reviewer : AssignmentRole.Candidate;
        if (role != UserRole.Admin && assignment?.Role != expected)
        {
            // Atanmadığı sınav "yok" gibi yanıtlanıyor; var olduğu bilgisi verilmiyor.
            return null;
        }

        var count = await db.Questions.CountAsync(q => q.ExamId == examId, ct);
        return ToSummary(exam, count, role, assignment?.AttemptUsedAt);
    }

    public async Task<ExamOperationResult> CreateAsync(ExamRequest request, CancellationToken ct = default)
    {
        if (ExamRules.Validate(request) is { } error)
        {
            return ExamOperationResult.Invalid(error);
        }

        var exam = new Exam { Name = request.Name!.Trim(), CreatedAt = clock.GetUtcNow() };
        Apply(exam, request);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(ct);
        return ExamOperationResult.Ok(exam.Id);
    }

    public async Task<ExamOperationResult> UpdateAsync(Guid examId, ExamRequest request, CancellationToken ct = default)
    {
        var exam = await db.Exams.SingleOrDefaultAsync(e => e.Id == examId, ct);
        if (exam is null)
        {
            return ExamOperationResult.NotFound;
        }

        if (ExamRules.Validate(request) is { } error)
        {
            return ExamOperationResult.Invalid(error);
        }

        if (await HasSessionsAsync(examId, ct))
        {
            return ExamOperationResult.Conflict(LockedMessage);
        }

        exam.Name = request.Name!.Trim();
        Apply(exam, request);
        await db.SaveChangesAsync(ct);
        return ExamOperationResult.Ok(exam.Id);
    }

    public async Task<ExamOperationResult> DeleteAsync(Guid examId, CancellationToken ct = default)
    {
        var exam = await db.Exams.SingleOrDefaultAsync(e => e.Id == examId, ct);
        if (exam is null)
        {
            return ExamOperationResult.NotFound;
        }

        // Oturumlar rapor ve itiraz için gerekli; veritabanında da Restrict (K-12).
        if (await HasSessionsAsync(examId, ct))
        {
            return ExamOperationResult.Conflict("Bu sınavda oturum başlamış; sınav silinemez.");
        }

        db.Exams.Remove(exam); // sorular ve atamalar Cascade (K-12)
        await db.SaveChangesAsync(ct);
        return ExamOperationResult.Ok();
    }

    /// <summary>Yönetici için sorular, doğru seçenekle birlikte, sırasına göre.</summary>
    public async Task<IReadOnlyList<QuestionDetail>?> ListQuestionsAsync(Guid examId, CancellationToken ct = default)
    {
        if (!await db.Exams.AnyAsync(e => e.Id == examId, ct))
        {
            return null;
        }

        var questions = await db.Questions.AsNoTracking()
            .Where(q => q.ExamId == examId)
            .OrderBy(q => q.Order).ThenBy(q => q.Id)
            .ToListAsync(ct);
        return questions.Select(ToDetail).ToList();
    }

    public async Task<ExamOperationResult> AddQuestionAsync(Guid examId, QuestionRequest request, CancellationToken ct = default)
    {
        if (!await db.Exams.AnyAsync(e => e.Id == examId, ct))
        {
            return ExamOperationResult.NotFound;
        }

        if (ExamRules.Validate(request) is { } error)
        {
            return ExamOperationResult.Invalid(error);
        }

        if (await HasSessionsAsync(examId, ct))
        {
            return ExamOperationResult.Conflict(LockedMessage);
        }

        // Sıra verilmezse sona ekleniyor.
        var order = request.Order ?? ((await db.Questions.Where(q => q.ExamId == examId).MaxAsync(q => (int?)q.Order, ct)) ?? 0) + 1;
        var question = new Question { ExamId = examId, Text = request.Text!.Trim(), Order = order };
        Apply(question, request);
        db.Questions.Add(question);
        await db.SaveChangesAsync(ct);
        return ExamOperationResult.Ok(question.Id);
    }

    /// <summary>Soruyu düzenler; sıralama da bu uçla (<c>order</c>) yapılıyor.</summary>
    public async Task<ExamOperationResult> UpdateQuestionAsync(Guid questionId, QuestionRequest request, CancellationToken ct = default)
    {
        var question = await db.Questions.SingleOrDefaultAsync(q => q.Id == questionId, ct);
        if (question is null)
        {
            return ExamOperationResult.NotFound;
        }

        if (ExamRules.Validate(request) is { } error)
        {
            return ExamOperationResult.Invalid(error);
        }

        if (await HasSessionsAsync(question.ExamId, ct))
        {
            return ExamOperationResult.Conflict(LockedMessage);
        }

        question.Text = request.Text!.Trim();
        question.Order = request.Order ?? question.Order;
        Apply(question, request);
        await db.SaveChangesAsync(ct);
        return ExamOperationResult.Ok(question.Id);
    }

    public async Task<ExamOperationResult> DeleteQuestionAsync(Guid questionId, CancellationToken ct = default)
    {
        var question = await db.Questions.SingleOrDefaultAsync(q => q.Id == questionId, ct);
        if (question is null)
        {
            return ExamOperationResult.NotFound;
        }

        // Cevaplanmış soru silinirse cevaplar anlamsızlaşır (K-12, Restrict).
        if (await HasSessionsAsync(question.ExamId, ct))
        {
            return ExamOperationResult.Conflict(LockedMessage);
        }

        db.Questions.Remove(question);
        await db.SaveChangesAsync(ct);
        return ExamOperationResult.Ok();
    }

    private Task<bool> HasSessionsAsync(Guid examId, CancellationToken ct) =>
        db.ExamSessions.AnyAsync(s => s.ExamId == examId, ct);

    private static void Apply(Exam exam, ExamRequest r)
    {
        exam.Type = r.Type!.Value;
        exam.DurationMinutes = r.DurationMinutes!.Value;
        // Hep UTC tutuluyor (K-15); istemcinin gönderdiği saat dilimi farkı korunmuyor.
        exam.WindowStartAt = r.WindowStartAt!.Value.ToUniversalTime();
        exam.WindowEndAt = r.WindowEndAt!.Value.ToUniversalTime();
        exam.RetentionDays = r.RetentionDays ?? 30; // GAB veri yaşam döngüsü: varsayılan 30 gün
    }

    private static void Apply(Question question, QuestionRequest r)
    {
        // Yeni liste: EF Core, aynı liste örneğindeki değişikliği her zaman fark etmiyor.
        question.Options = r.Options!.Select(o => o.Trim()).ToList();
        question.CorrectOptionIndex = r.CorrectOptionIndex!.Value;
    }

    private static ExamSummary ToSummary(Exam e, int questionCount, UserRole role, DateTimeOffset? attemptUsedAt) => new(
        e.Id, e.Name, e.Type, e.DurationMinutes, e.WindowStartAt, e.WindowEndAt,
        ExamRules.LatestStartAt(e), e.RetentionDays, questionCount,
        role == UserRole.Candidate ? attemptUsedAt is not null : null);

    private static QuestionDetail ToDetail(Question q) => new(q.Id, q.Text, q.Options, q.CorrectOptionIndex, q.Order);
}
