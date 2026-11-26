using AIGuard.Application.Abstractions;
using AIGuard.Application.Review;
using AIGuard.Application.Risk;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Persistence;
using AIGuard.IntegrationTests.Events;
using Microsoft.Extensions.Time.Testing;

namespace AIGuard.IntegrationTests.Review;

/// <summary>Bir sınav, ona atanmış ve atanmamış değerlendirici, Admin ve sınava ait oturumlar.</summary>
internal sealed record ReviewScenario(Exam Exam, User Reviewer, User OtherReviewer, User Admin)
{
    public ReviewActor AsReviewer => new(Reviewer.Id, UserRole.Reviewer);

    public ReviewActor AsOtherReviewer => new(OtherReviewer.Id, UserRole.Reviewer);

    public ReviewActor AsAdmin => new(Admin.Id, UserRole.Admin);
}

/// <summary>Oturuma eklenecek olay: sınavın başından itibaren dakika ve süre.</summary>
internal sealed record EventSpec(EventType Type, double AtMinute, double DurationSec = 0, bool WithEvidence = false);

internal static class ReviewTestHelpers
{
    /// <summary>Sınavların başladığı an; sınavlar 30 dakika.</summary>
    public static readonly DateTimeOffset ExamStart = EventTestHelpers.Now;

    /// <summary>
    /// İnceleme anı: sınavlar bitmiş ve son başlangıç zamanı geçmiş (pencere ExamStart + 1 saatte bitiyor,
    /// süre 30 dk; son başlangıç ExamStart + 30 dk).
    /// </summary>
    public static FakeTimeProvider Clock() => new(ExamStart.AddHours(2));

    public static readonly RiskEngine Risk = new(EventTestHelpers.Options());

    public static ReviewService Reviews(AppDbContext db, TimeProvider clock) => new(db, EventTestHelpers.Risks(db, clock), new EvidenceLinkSigner(clock), clock);

    public static ReportService Reports(AppDbContext db, TimeProvider clock) => new(db, EventTestHelpers.Risks(db, clock), clock);

    public static async Task<ReviewScenario> ScenarioAsync(AppDbContext db)
    {
        var exam = new Exam
        {
            Name = "İnceleme Sınavı",
            Type = ExamType.GeneralAptitude,
            DurationMinutes = 30,
            WindowStartAt = ExamStart.AddDays(-1),
            WindowEndAt = ExamStart.AddHours(1),
        };
        var reviewer = TestData.UserWithRole(UserRole.Reviewer);
        var other = TestData.UserWithRole(UserRole.Reviewer);
        var admin = TestData.UserWithRole(UserRole.Admin);
        db.AddRange(exam, reviewer, other, admin);
        db.ExamAssignments.Add(new ExamAssignment { Exam = exam, User = reviewer, Role = AssignmentRole.Reviewer });

        // Diğer değerlendirici başka bir sınava atanmış: değerlendirici olması yetmiyor, atama gerekiyor.
        var otherExam = TestData.Exam();
        db.ExamAssignments.Add(new ExamAssignment { Exam = otherExam, User = other, Role = AssignmentRole.Reviewer });
        await db.SaveChangesAsync();
        return new ReviewScenario(exam, reviewer, other, admin);
    }

    /// <summary>
    /// Sınava ait oturum. Tamamlanmış oturumlarda skor, risk motoruyla bitiş anına göre hesaplanıp yazılıyor
    /// (E4'teki teslimde olduğu gibi); olayların kayıtlı katkısı da aynı hesaptan.
    /// </summary>
    public static async Task<ExamSession> SessionAsync(
        AppDbContext db, Exam exam, SessionState state, SessionFlags flags = SessionFlags.None,
        string candidateName = "Test Aday", params EventSpec[] events)
    {
        var candidate = TestData.Candidate();
        candidate.FullName = candidateName;
        var started = state is SessionState.Completed or SessionState.Decided;
        var session = new ExamSession
        {
            ExamId = exam.Id,
            Candidate = candidate,
            State = state,
            Flags = flags,
            CreatedAt = ExamStart.AddMinutes(-5),
            StartedAt = started ? ExamStart : null,
            EndsAt = started ? ExamStart.AddMinutes(30) : null,
            EndedAt = started ? ExamStart.AddMinutes(30) : null,
        };
        db.ExamSessions.Add(session);
        db.ExamAssignments.Add(new ExamAssignment { ExamId = exam.Id, User = candidate, Role = AssignmentRole.Candidate });

        var created = events.Select(spec =>
        {
            var e = new Event
            {
                ExamSession = session,
                Type = spec.Type,
                Source = EventSource.System,
                StartedAt = ExamStart.AddMinutes(spec.AtMinute),
                DurationSec = spec.DurationSec,
                ReceivedAt = ExamStart.AddMinutes(spec.AtMinute),
                Reason = $"{spec.AtMinute}. dakikada {spec.Type}",
            };
            if (spec.WithEvidence)
            {
                e.EvidenceFrame = new EvidenceFrame { StorageKey = $"{Guid.NewGuid():N}.bin", DeleteAfter = ExamStart.AddDays(30) };
            }

            return e;
        }).ToList();
        db.Events.AddRange(created);

        var result = Risk.Calculate(created.Select(e => new RiskEventInput(e.Id, e.Type, e.StartedAt, e.DurationSec, e.ReceivedAt)),
            session.EndedAt ?? ExamStart);
        foreach (var c in result.Contributions)
        {
            created.Single(e => e.Id == c.EventId).ScoreContribution = c.BaseContribution;
        }

        session.RiskScore = result.Score;
        await db.SaveChangesAsync();
        return session;
    }
}

/// <summary>Gönderilen bildirimleri biriktiren sahte bildirimci.</summary>
internal sealed class RecordingReviewNotifier : IReviewNotifier
{
    public List<SessionReadyForReview> Messages { get; } = [];

    public Task SessionReadyForReviewAsync(SessionReadyForReview message, CancellationToken ct = default)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }
}
