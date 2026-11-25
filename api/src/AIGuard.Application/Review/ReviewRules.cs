using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Exams;
using AIGuard.Application.Risk;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Review;

/// <summary>İnceleme ve rapor servislerinin ortak kuralları (K-31).</summary>
internal static class ReviewRules
{
    /// <summary>Sınav başlamadan önceki adımlar; son başlangıç zamanı geçince oturum "başlamadı" sayılıyor (S-16).</summary>
    public static readonly SessionState[] PreStart =
        [SessionState.ConsentPending, SessionState.AgentPairing, SessionState.IdentityVerification];

    /// <summary>İnceleme listesine girebilecek durumlar; süren ve iptal edilen oturumlar listede yok.</summary>
    public static readonly SessionState[] Listed = [SessionState.Completed, SessionState.Decided, .. PreStart];

    /// <summary>Oturum listede görünmüyorsa (sürüyor, iptal edildi ya da hâlâ başlayabilir) <c>null</c>.</summary>
    public static ReviewStatus? Status(SessionState state, SessionFlags flags, Exam exam, DateTimeOffset now) => state switch
    {
        SessionState.Decided => ReviewStatus.Decided,
        SessionState.Completed => flags != SessionFlags.None ? ReviewStatus.AwaitingReview : ReviewStatus.Completed,
        _ when PreStart.Contains(state) && now > ExamRules.LatestStartAt(exam) => ReviewStatus.NotStarted,
        _ => null,
    };

    public static IEnumerable<RiskEventInput> RiskInputs(IEnumerable<Event> events) =>
        events.Select(e => new RiskEventInput(e.Id, e.Type, e.StartedAt, e.DurationSec, e.ReceivedAt, e.IsFalseAlarm));

    /// <summary>
    /// Skorun değerlendirildiği an: sınav bitişi (skor bitişte sabitleniyor, K-17); bitmemiş oturumda şimdi.
    /// </summary>
    public static DateTimeOffset EvaluatedAt(ExamSession session, DateTimeOffset now) => session.EndedAt ?? now;

    /// <summary>Atanmış değerlendirici okur ve yazar; Admin her sınavı yalnızca okur (K-31).</summary>
    public static Task<bool> CanReadExamAsync(IAppDbContext db, Guid examId, ReviewActor actor, CancellationToken ct) =>
        actor.Role switch
        {
            UserRole.Admin => db.Exams.AnyAsync(e => e.Id == examId, ct),
            UserRole.Reviewer => IsAssignedReviewerAsync(db, examId, actor.UserId, ct),
            _ => Task.FromResult(false),
        };

    public static Task<bool> CanWriteExamAsync(IAppDbContext db, Guid examId, ReviewActor actor, CancellationToken ct) =>
        actor.Role == UserRole.Reviewer ? IsAssignedReviewerAsync(db, examId, actor.UserId, ct) : Task.FromResult(false);

    private static Task<bool> IsAssignedReviewerAsync(IAppDbContext db, Guid examId, Guid userId, CancellationToken ct) =>
        db.ExamAssignments.AnyAsync(a => a.ExamId == examId && a.UserId == userId && a.Role == AssignmentRole.Reviewer, ct);

    public static ReviewDecisionView? Decision(ReviewerDecision? d) =>
        d is null ? null : new(d.Decision, d.Note, d.Reviewer.FullName, d.DecidedAt);

    public static IReadOnlyList<ReviewNoteView> Notes(IEnumerable<ReviewerNote> notes) =>
        [.. notes.OrderBy(n => n.CreatedAt).Select(n => new ReviewNoteView(n.Id, n.Text, n.Author.FullName, n.CreatedAt))];

    /// <summary>
    /// Kişisel veri içeren ekranın açıldığını denetim kaydına yazar (A5, K-32): kim, hangi oturuma, ne zaman baktı.
    /// Kanıt karesinin kendisi girişsiz imzalı bağlantıyla açıldığı için kayıt bağlantının verildiği bu anda tutuluyor.
    /// </summary>
    public static async Task AuditViewAsync(
        IAppDbContext db, string action, Guid sessionId, ReviewActor actor, DateTimeOffset now, object? details, CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actor.UserId,
            Action = action,
            TargetType = nameof(ExamSession),
            TargetId = sessionId,
            NewValue = details is null ? null : JsonSerializer.Serialize(details),
            OccurredAt = now,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Boşlukları kırpılmış metin; boşsa <c>null</c>.</summary>
    public static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
