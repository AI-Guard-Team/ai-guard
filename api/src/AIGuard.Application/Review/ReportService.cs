using AIGuard.Application.Abstractions;
using AIGuard.Application.Risk;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Review;

/// <summary>
/// Aday bazında sınav raporu (A4, E6'daki rapor sayfası): sınav sonucu, risk özeti, karar ve notlar.
/// Erişim inceleme uçlarıyla aynı (K-31). Olay zaman çizelgesi ve kanıt kareleri detay ucunda
/// (<see cref="ReviewService.GetAsync"/>); rapor özet veriyor.
/// </summary>
public sealed class ReportService(IAppDbContext db, ExamRiskService risks, TimeProvider clock)
{
    // Rapordaki "en önemli gerekçeler" listesinin uzunluğu.
    private const int TopReasonCount = 3;

    /// <param name="exportFormat">
    /// Dosya olarak indiriliyorsa biçimi (FR-34). Denetim kaydına <c>report.view</c> yerine <c>report.export</c>
    /// yazılıyor: kişisel veri sistemin dışına çıkıyor.
    /// </param>
    public async Task<CandidateReport?> GetAsync(
        Guid examId, Guid sessionId, ReviewActor actor, ReportFormat? exportFormat = null, CancellationToken ct = default)
    {
        var session = await db.ExamSessions.AsNoTracking()
            .Include(s => s.Exam)
            .Include(s => s.Candidate)
            .Include(s => s.ReviewerDecision!).ThenInclude(d => d.Reviewer)
            .SingleOrDefaultAsync(s => s.Id == sessionId && s.ExamId == examId, ct);

        var now = clock.GetUtcNow();
        var reviewStatus = session is null ? null : ReviewRules.Status(session.State, session.Flags, session.Exam, now);
        if (session is null || reviewStatus is not { } status || !await ReviewRules.CanReadExamAsync(db, examId, actor, ct))
        {
            return null;
        }

        // Koleksiyonlar ayrı sorgularla (soru × cevap × olay çarpımı olmasın).
        session.Exam.Questions = await db.Questions.AsNoTracking().Where(q => q.ExamId == examId).ToListAsync(ct);
        session.Answers = await db.Answers.AsNoTracking().Where(a => a.ExamSessionId == sessionId).ToListAsync(ct);
        session.Events = await db.Events.AsNoTracking().Where(e => e.ExamSessionId == sessionId).ToListAsync(ct);
        session.ReviewerNotes = await db.ReviewerNotes.AsNoTracking()
            .Where(n => n.ExamSessionId == sessionId).Include(n => n.Author).ToListAsync(ct);

        var risk = await risks.EngineForExamAsync(examId, ct);
        var contributions = risk.Calculate(ReviewRules.RiskInputs(session.Events), ReviewRules.EvaluatedAt(session, now))
            .Contributions.ToDictionary(c => c.EventId, c => c.Contribution);

        var byType = session.Events
            .GroupBy(e => e.Type)
            .Select(g => new EventTypeSummary(
                g.Key, g.Count(), g.Count(e => e.IsFalseAlarm),
                Math.Round(g.Sum(e => contributions.GetValueOrDefault(e.Id)), 4)))
            .OrderByDescending(s => s.Contribution).ThenBy(s => s.Type)
            .ToList();

        var topReasons = session.Events
            .Where(e => e.Reason is not null && contributions.GetValueOrDefault(e.Id) > 0)
            .OrderByDescending(e => contributions[e.Id])
            .Take(TopReasonCount)
            .Select(e => ReasonFormatter.Localize(e.Reason)!)
            .ToList();

        await ReviewRules.AuditViewAsync(db, exportFormat is null ? "report.view" : "report.export", session.Id, actor, now,
            exportFormat is { } format ? new { format = format.ToString() } : null, ct);

        return new CandidateReport(
            session.Id, session.ExamId, session.Exam.Name, session.Exam.Type, session.Exam.DurationMinutes,
            session.CandidateId, session.Candidate.FullName, session.Candidate.Email,
            session.State, status, session.CreatedAt, session.StartedAt, session.EndedAt,
            // Sınav hiç başlamadıysa sonuç yok; "0 doğru" yanıltıcı olurdu.
            status == ReviewStatus.NotStarted ? null : Result(session),
            session.RiskScore, SessionFlagList.Of(session.Flags), topReasons, byType,
            session.VerificationAttempts, ReviewRules.Decision(session.ReviewerDecision), ReviewRules.Notes(session.ReviewerNotes),
            // Doluysa olay özeti ve gerekçeler saklama süresi dolduğu için boş; skor ve karar kayıtlı hâliyle duruyor.
            session.MonitoringDataDeletedAt);
    }

    /// <summary>Her soru için en yüksek seq'li cevap geçerli (E4); seçeneği boş olan cevap "boş" sayılıyor.</summary>
    internal static ExamResultView Result(Domain.Entities.ExamSession session)
    {
        var finalAnswers = session.Answers
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.MaxBy(a => a.Seq)!);

        int correct = 0, wrong = 0, blank = 0, unmonitored = 0;
        foreach (var question in session.Exam.Questions)
        {
            if (!finalAnswers.TryGetValue(question.Id, out var answer) || answer.SelectedOption is null)
            {
                blank++;
                continue;
            }

            if (answer.SelectedOption == question.CorrectOptionIndex)
            {
                correct++;
            }
            else
            {
                wrong++;
            }

            if (answer.Unmonitored)
            {
                unmonitored++;
            }
        }

        return new ExamResultView(session.Exam.Questions.Count, correct, wrong, blank, unmonitored);
    }
}
