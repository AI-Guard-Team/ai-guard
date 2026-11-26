using System.Globalization;
using System.Text;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Localization;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Review;

public sealed record ExamDecisionCounts(int Clean, int Suspicious, int Violation);

public sealed record ExamEventTypeCount(EventType Type, int Count, int FalseAlarms);

/// <summary>Sınav başından itibaren dakika başına olay sayısı (yanlış alarmlar hariç).</summary>
public sealed record ExamMinuteDensity(int Minute, int Events);

/// <param name="Assigned">Sınava atanmış aday sayısı.</param>
/// <param name="Started">Kimliği doğrulanıp sınava başlamış (InProgress'e geçmiş) oturum.</param>
/// <param name="Completed">Teslim edilmiş ya da karara bağlanmış.</param>
/// <param name="AverageRisk">Tamamlanan oturumların ortalama risk skoru; tamamlanan yoksa 0.</param>
public sealed record ExamSummaryReport(
    Guid ExamId,
    string ExamName,
    int DurationMinutes,
    int Assigned,
    int Started,
    int InProgress,
    int Completed,
    int AwaitingReview,
    int Decided,
    int Risky,
    double AverageRisk,
    double MaxRisk,
    double AverageCorrect,
    int QuestionCount,
    ExamDecisionCounts Decisions,
    IReadOnlyList<ExamEventTypeCount> Events,
    IReadOnlyList<ExamMinuteDensity> Density);

/// <summary>
/// Sınav düzeyinde toplu rapor (K-49, Stitch 30): katılım, durumlar, karar dağılımı, risk ve olay yoğunluğu; ayrıca
/// bütün oturumlar tek CSV. Erişim aday raporuyla aynı (yönetici ya da atanmış değerlendirici, K-31); görüntüleme
/// ve indirme denetim kaydına yazılıyor.
/// </summary>
public sealed class ExamReportService(IAppDbContext db, TimeProvider clock)
{
    public async Task<ExamSummaryReport?> GetAsync(Guid examId, ReviewActor actor, CancellationToken ct = default)
    {
        var exam = await db.Exams.AsNoTracking().SingleOrDefaultAsync(e => e.Id == examId, ct);
        if (exam is null || !await ReviewRules.CanReadExamAsync(db, examId, actor, ct))
        {
            return null;
        }

        var sessions = await db.ExamSessions.AsNoTracking()
            .Where(s => s.ExamId == examId)
            .Include(s => s.ReviewerDecision)
            .ToListAsync(ct);
        var assigned = await db.ExamAssignments.CountAsync(a => a.ExamId == examId && a.Role == AssignmentRole.Candidate, ct);
        var questionCount = await db.Questions.CountAsync(q => q.ExamId == examId, ct);

        var now = clock.GetUtcNow();
        var completed = sessions.Where(s => s.State is SessionState.Completed or SessionState.Decided).ToList();
        var statuses = sessions.Select(s => ReviewRules.Status(s.State, s.Flags, exam, now)).ToList();

        var sessionIds = completed.Select(s => s.Id).ToList();
        var events = await db.Events.AsNoTracking()
            .Where(e => sessionIds.Contains(e.ExamSessionId))
            .Select(e => new { e.ExamSessionId, e.Type, e.StartedAt, e.IsFalseAlarm })
            .ToListAsync(ct);

        var startedAt = completed.Where(s => s.StartedAt is not null).ToDictionary(s => s.Id, s => s.StartedAt!.Value);
        var density = new int[Math.Max(1, exam.DurationMinutes)];
        foreach (var e in events.Where(e => !e.IsFalseAlarm && startedAt.ContainsKey(e.ExamSessionId)))
        {
            var minute = (int)Math.Floor((e.StartedAt - startedAt[e.ExamSessionId]).TotalMinutes);
            if (minute >= 0 && minute < density.Length)
            {
                density[minute]++;
            }
        }

        var averageCorrect = completed.Count == 0 ? 0 : await AverageCorrectAsync(examId, completed, ct);
        var decisions = completed.Where(s => s.ReviewerDecision is not null).Select(s => s.ReviewerDecision!.Decision).ToList();

        await AuditAsync(actor, "report.exam_view", examId, now, ct);

        return new ExamSummaryReport(
            exam.Id, exam.Name, exam.DurationMinutes,
            Assigned: assigned,
            Started: sessions.Count(s => s.StartedAt is not null),
            InProgress: sessions.Count(s => s.State is SessionState.InProgress or SessionState.PermissionPending or SessionState.Disconnected),
            Completed: completed.Count,
            AwaitingReview: statuses.Count(s => s == ReviewStatus.AwaitingReview),
            Decided: statuses.Count(s => s == ReviewStatus.Decided),
            Risky: completed.Count(s => s.Flags.HasFlag(SessionFlags.Risky)),
            AverageRisk: completed.Count == 0 ? 0 : Math.Round(completed.Average(s => s.RiskScore), 2),
            MaxRisk: completed.Count == 0 ? 0 : Math.Round(completed.Max(s => s.RiskScore), 2),
            AverageCorrect: averageCorrect,
            QuestionCount: questionCount,
            Decisions: new ExamDecisionCounts(
                decisions.Count(d => d == ReviewDecisionType.Clean),
                decisions.Count(d => d == ReviewDecisionType.Suspicious),
                decisions.Count(d => d == ReviewDecisionType.Violation)),
            Events: events
                .GroupBy(e => e.Type)
                .Select(g => new ExamEventTypeCount(g.Key, g.Count(), g.Count(e => e.IsFalseAlarm)))
                .OrderByDescending(e => e.Count)
                .ThenBy(e => e.Type.ToString(), StringComparer.Ordinal)
                .ToList(),
            Density: [.. density.Select((count, minute) => new ExamMinuteDensity(minute + 1, count))]);
    }

    /// <summary>
    /// Bütün oturumlar tek CSV (Excel'de açılır; UTF-8 BOM, ayırıcı noktalı virgül). Hücre "=", "+", "-", "@" ile
    /// başlıyorsa önüne ' konuyor: aday adı formül olarak çalışmasın (CSV enjeksiyonu).
    /// </summary>
    public async Task<(byte[] Content, string FileName)?> ExportCsvAsync(Guid examId, ReviewActor actor, CancellationToken ct = default)
    {
        var exam = await db.Exams.AsNoTracking().Include(e => e.Questions).SingleOrDefaultAsync(e => e.Id == examId, ct);
        if (exam is null || !await ReviewRules.CanReadExamAsync(db, examId, actor, ct))
        {
            return null;
        }

        var sessions = await db.ExamSessions.AsNoTracking()
            .Where(s => s.ExamId == examId)
            .Include(s => s.Candidate)
            .Include(s => s.Answers)
            .Include(s => s.ReviewerDecision!).ThenInclude(d => d.Reviewer)
            .OrderBy(s => s.Candidate.FullName)
            .ToListAsync(ct);

        var english = ApiLanguage.IsEnglish;
        var header = english
            ? new[] { "Candidate", "Email", "State", "Started", "Ended", "Correct", "Wrong", "Blank", "Risk score", "Risky", "Flags", "Decision", "Reviewer" }
            : new[] { "Aday", "E-posta", "Durum", "Başlangıç", "Bitiş", "Doğru", "Yanlış", "Boş", "Risk skoru", "Riskli", "İşaretler", "Karar", "Değerlendirici" };

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(';', header.Select(Cell)));
        foreach (var s in sessions)
        {
            s.Exam = exam;
            var result = s.StartedAt is null ? null : ReportService.Result(s);
            csv.AppendLine(string.Join(';', new[]
            {
                s.Candidate.FullName,
                s.Candidate.Email,
                s.State.ToString(),
                Stamp(s.StartedAt),
                Stamp(s.EndedAt),
                result?.Correct.ToString(CultureInfo.InvariantCulture) ?? "",
                result?.Wrong.ToString(CultureInfo.InvariantCulture) ?? "",
                result?.Blank.ToString(CultureInfo.InvariantCulture) ?? "",
                s.RiskScore.ToString("0.##", CultureInfo.InvariantCulture),
                s.Flags.HasFlag(SessionFlags.Risky) ? (english ? "yes" : "evet") : (english ? "no" : "hayır"),
                string.Join(' ', SessionFlagList.Of(s.Flags)),
                s.ReviewerDecision?.Decision.ToString() ?? "",
                s.ReviewerDecision?.Reviewer.FullName ?? "",
            }.Select(Cell)));
        }

        var now = clock.GetUtcNow();
        await AuditAsync(actor, "report.exam_export", examId, now, ct);

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return (bytes, $"aiguard-sinav-{exam.Id.ToString("N")[..8]}-{now:yyyyMMdd}.csv");
    }

    private async Task<double> AverageCorrectAsync(Guid examId, List<ExamSession> completed, CancellationToken ct)
    {
        var questions = await db.Questions.AsNoTracking().Where(q => q.ExamId == examId).ToListAsync(ct);
        var ids = completed.Select(s => s.Id).ToList();
        var answers = await db.Answers.AsNoTracking().Where(a => ids.Contains(a.ExamSessionId)).ToListAsync(ct);
        var byQuestion = questions.ToDictionary(q => q.Id);

        var totals = answers
            .GroupBy(a => new { a.ExamSessionId, a.QuestionId })
            .Select(g => g.MaxBy(a => a.Seq)!)
            .Where(a => a.SelectedOption is not null && byQuestion.TryGetValue(a.QuestionId, out var q) && q.CorrectOptionIndex == a.SelectedOption)
            .GroupBy(a => a.ExamSessionId)
            .ToDictionary(g => g.Key, g => g.Count());

        return Math.Round(completed.Average(s => (double)totals.GetValueOrDefault(s.Id)), 2);
    }

    private async Task AuditAsync(ReviewActor actor, string action, Guid examId, DateTimeOffset now, CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog { ActorUserId = actor.UserId, Action = action, TargetType = nameof(Exam), TargetId = examId, OccurredAt = now });
        await db.SaveChangesAsync(ct);
    }

    private static string Stamp(DateTimeOffset? at) => at?.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture) ?? "";

    private static string Cell(string value)
    {
        var safe = value.Length > 0 && "=+-@\t\r".Contains(value[0]) ? "'" + value : value;
        return safe.Contains(';') || safe.Contains('"') || safe.Contains('\n') ? "\"" + safe.Replace("\"", "\"\"") + "\"" : safe;
    }
}
