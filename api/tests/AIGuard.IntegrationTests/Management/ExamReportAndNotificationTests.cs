using System.Text;
using AIGuard.Application.Notifications;
using AIGuard.Application.Review;
using AIGuard.Application.Risk;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.IntegrationTests.Events;
using AIGuard.IntegrationTests.Review;
using Microsoft.EntityFrameworkCore;
using static AIGuard.IntegrationTests.Review.ReviewTestHelpers;

namespace AIGuard.IntegrationTests.Management;

/// <summary>K-49: sınav düzeyinde toplu rapor, bildirimler ve sınava özel risk ayarının skora etkisi.</summary>
public class ExamReportAndNotificationTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    [SkippableFact]
    public async Task Exam_summary_counts_sessions_decisions_events_and_density()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky, events: [new EventSpec(EventType.PHONE, 2, 8), new EventSpec(EventType.TAB_BLUR, 2)]);
        await SessionAsync(db, s.Exam, SessionState.Completed);
        var decided = await SessionAsync(db, s.Exam, SessionState.Decided, SessionFlags.Indicated, events: [new EventSpec(EventType.TAB_BLUR, 10)]);
        db.ReviewerDecisions.Add(new ReviewerDecision { ExamSessionId = decided.Id, ReviewerId = s.Reviewer.Id, Decision = ReviewDecisionType.Violation });
        await db.SaveChangesAsync();

        var report = await new ExamReportService(db, Clock()).GetAsync(s.Exam.Id, s.AsReviewer);

        Assert.NotNull(report);
        Assert.Equal(3, report.Assigned);
        Assert.Equal(3, report.Completed);
        Assert.Equal(1, report.AwaitingReview);
        Assert.Equal(1, report.Decided);
        Assert.Equal(1, report.Risky);
        Assert.Equal(new ExamDecisionCounts(0, 0, 1), report.Decisions);
        Assert.Equal(2, report.Events.Single(e => e.Type == EventType.TAB_BLUR).Count);
        Assert.Equal(30, report.Density.Count);
        Assert.Equal(2, report.Density.Single(d => d.Minute == 3).Events); // 2. dakikada başlayan iki olay → 3. dakika dilimi
        Assert.Equal(1, report.Density.Single(d => d.Minute == 11).Events);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "report.exam_view" && a.TargetId == s.Exam.Id));
    }

    [SkippableFact]
    public async Task Csv_export_starts_with_bom_and_neutralizes_formulas()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        await SessionAsync(db, s.Exam, SessionState.Completed, candidateName: "=HYPERLINK(\"http://kotu.site\";\"tikla\")");

        var file = await new ExamReportService(db, Clock()).ExportCsvAsync(s.Exam.Id, s.AsAdmin);

        Assert.NotNull(file);
        var bytes = file.Value.Content;
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes[3..]);
        Assert.StartsWith("Aday;E-posta;Durum", text);
        Assert.Contains("\"'=HYPERLINK(\"\"http://kotu.site\"\";\"\"tikla\"\")\"", text); // ' ile metne çevrildi, ; yüzünden tırnaklı
        Assert.DoesNotContain(";=HYPERLINK", text);
        Assert.EndsWith(".csv", file.Value.FileName);
    }

    [SkippableFact]
    public async Task Unassigned_reviewer_gets_no_summary_or_export()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        var service = new ExamReportService(db, Clock());

        Assert.Null(await service.GetAsync(s.Exam.Id, s.AsOtherReviewer));
        Assert.Null(await service.ExportCsvAsync(s.Exam.Id, s.AsOtherReviewer));
    }

    [SkippableFact]
    public async Task Reviewer_is_notified_of_waiting_file_until_it_is_decided()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        var session = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky, candidateName: "Bekleyen Aday");
        var service = new NotificationService(db, Clock());

        var before = await service.ListAsync(s.Reviewer.Id, UserRole.Reviewer);
        var other = await service.ListAsync(s.OtherReviewer.Id, UserRole.Reviewer);
        session.State = SessionState.Decided;
        await db.SaveChangesAsync();
        var after = await service.ListAsync(s.Reviewer.Id, UserRole.Reviewer);

        var item = Assert.Single(before);
        Assert.Equal(NotificationKind.SessionAwaitingReview, item.Kind);
        Assert.Equal("Bekleyen Aday", item.CandidateName);
        Assert.Equal($"review:{session.Id}", item.Id);
        Assert.Empty(other); // atanmamış değerlendirici başka sınavın dosyasını görmez
        Assert.Empty(after);
    }

    [SkippableFact]
    public async Task Candidate_is_notified_of_open_exam_and_missing_photo()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var clock = Clock();
        var now = clock.GetUtcNow();
        var candidate = TestData.Candidate();
        var open = new Exam { Name = "Açık Sınav", Type = ExamType.GeneralAptitude, DurationMinutes = 30, WindowStartAt = now.AddMinutes(-10), WindowEndAt = now.AddHours(3) };
        var soon = new Exam { Name = "Yarınki Sınav", Type = ExamType.GeneralAptitude, DurationMinutes = 30, WindowStartAt = now.AddHours(5), WindowEndAt = now.AddHours(8) };
        var later = new Exam { Name = "Gelecek Ay", Type = ExamType.GeneralAptitude, DurationMinutes = 30, WindowStartAt = now.AddDays(30), WindowEndAt = now.AddDays(31) };
        db.AddRange(candidate, open, soon, later);
        foreach (var exam in new[] { open, soon, later })
        {
            db.ExamAssignments.Add(new ExamAssignment { Exam = exam, User = candidate, Role = AssignmentRole.Candidate });
        }

        await db.SaveChangesAsync();

        var items = await new NotificationService(db, clock).ListAsync(candidate.Id, UserRole.Candidate);

        Assert.Contains(items, i => i.Kind == NotificationKind.ExamOpenNow && i.ExamId == open.Id);
        Assert.Contains(items, i => i.Kind == NotificationKind.ExamOpensSoon && i.ExamId == soon.Id);
        Assert.DoesNotContain(items, i => i.ExamId == later.Id);
        Assert.Contains(items, i => i.Kind == NotificationKind.PhotoMissing && i.Count == 3);
    }

    [SkippableFact]
    public async Task Admin_is_notified_of_upcoming_exam_without_questions_or_candidates()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var clock = Clock();
        var now = clock.GetUtcNow();
        var empty = new Exam { Name = "Boş Sınav", Type = ExamType.GeneralAptitude, DurationMinutes = 30, WindowStartAt = now.AddHours(10), WindowEndAt = now.AddHours(12) };
        db.Exams.Add(empty);
        await db.SaveChangesAsync();

        var items = await new NotificationService(db, clock).ListAsync(Guid.NewGuid(), UserRole.Admin);

        Assert.Contains(items, i => i.Kind == NotificationKind.ExamWithoutQuestions && i.ExamId == empty.Id);
        Assert.Contains(items, i => i.Kind == NotificationKind.ExamWithoutCandidates && i.ExamId == empty.Id);
    }

    [SkippableFact]
    public async Task Exam_specific_weight_changes_live_score()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var clock = EventTestHelpers.Clock();
        var plain = await EventTestHelpers.InProgressSessionAsync(db);
        var custom = await EventTestHelpers.InProgressSessionAsync(db);
        db.RiskSettings.Add(new RiskSetting { ExamId = custom.ExamId, EventType = EventType.PHONE, Weight = 20, Decay = 0, Threshold = 0 });
        await db.SaveChangesAsync();

        var events = EventTestHelpers.Service(db, clock);
        var a = await events.RecordSystemEventAsync(plain.Id, new AIGuard.Application.Events.SystemEvent(EventType.PHONE, clock.GetUtcNow()));
        var b = await events.RecordSystemEventAsync(custom.Id, new AIGuard.Application.Events.SystemEvent(EventType.PHONE, clock.GetUtcNow()));

        Assert.Equal(5, a.Event!.ScoreContribution, 3);  // test ayarındaki PHONE ağırlığı
        Assert.Equal(20, b.Event!.ScoreContribution, 3); // sınava özel ağırlık
    }
}
