using AIGuard.Application.Review;
using AIGuard.Application.Sessions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.IntegrationTests.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using static AIGuard.IntegrationTests.Review.ReviewTestHelpers;

namespace AIGuard.IntegrationTests.Review;

/// <summary>A4: inceleme listesi, detay, karar, not, yanlış alarm ve aday raporu.</summary>
public class ReviewServiceTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    [SkippableFact]
    public async Task List_puts_sessions_awaiting_review_first_and_counts_them()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        var clean = await SessionAsync(db, s.Exam, SessionState.Completed, events: new EventSpec(EventType.TAB_BLUR, 5));
        var risky = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky,
            events: [new EventSpec(EventType.TAB_BLUR, 3), new EventSpec(EventType.PHONE, 10, 8)]);
        var noAgent = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.NoAgent);
        var decided = await SessionAsync(db, s.Exam, SessionState.Decided, SessionFlags.Risky);
        var stuck = await SessionAsync(db, s.Exam, SessionState.IdentityVerification);
        await SessionAsync(db, s.Exam, SessionState.InProgress);
        await SessionAsync(db, s.Exam, SessionState.Cancelled);

        var list = await Reviews(db, Clock()).ListAsync(s.Exam.Id, s.AsReviewer, new ReviewListQuery());

        Assert.NotNull(list);
        Assert.Equal(2, list.PendingCount);
        Assert.Equal([risky.Id, noAgent.Id, decided.Id, clean.Id, stuck.Id], list.Sessions.Select(r => r.SessionId));
        Assert.Equal(
            [ReviewStatus.AwaitingReview, ReviewStatus.AwaitingReview, ReviewStatus.Decided, ReviewStatus.Completed, ReviewStatus.NotStarted],
            list.Sessions.Select(r => r.Status));

        // Kısa gerekçe skora en çok katkı yapan olaydan; işaretler liste olarak.
        var top = list.Sessions[0];
        Assert.Equal("10. dakikada PHONE", top.TopReason);
        Assert.Equal([SessionFlags.Risky], top.Flags);
        Assert.Equal(ExamStart, top.StartedAt);
    }

    [SkippableFact]
    public async Task List_filters_by_risky_and_flags_and_sorts_by_score()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        var high = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky, events: new EventSpec(EventType.PHONE, 25, 30));
        var low = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky | SessionFlags.Indicated, events: new EventSpec(EventType.TAB_BLUR, 1));
        var noAgent = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.NoAgent);
        var service = Reviews(db, Clock());

        var risky = await service.ListAsync(s.Exam.Id, s.AsReviewer, new ReviewListQuery(Risky: true, Sort: ReviewSort.Score));
        var notRisky = await service.ListAsync(s.Exam.Id, s.AsReviewer, new ReviewListQuery(Risky: false));
        var byFlag = await service.ListAsync(s.Exam.Id, s.AsReviewer, new ReviewListQuery(Flags: [SessionFlags.Indicated, SessionFlags.NoAgent]));

        Assert.Equal([high.Id, low.Id], risky!.Sessions.Select(r => r.SessionId));
        Assert.Equal([noAgent.Id], notRisky!.Sessions.Select(r => r.SessionId));
        Assert.Equal(3, byFlag!.PendingCount); // sayı filtreden bağımsız
        Assert.Equal([low.Id, noAgent.Id], byFlag.Sessions.Select(r => r.SessionId));
    }

    [SkippableFact]
    public async Task Only_assigned_reviewer_and_admin_can_read()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        var session = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky);
        var service = Reviews(db, Clock());
        var candidate = new ReviewActor(session.CandidateId, UserRole.Candidate);

        Assert.NotNull(await service.ListAsync(s.Exam.Id, s.AsAdmin, new ReviewListQuery()));
        Assert.NotNull(await service.GetAsync(session.Id, s.AsAdmin));
        Assert.Null(await service.ListAsync(s.Exam.Id, s.AsOtherReviewer, new ReviewListQuery()));
        Assert.Null(await service.GetAsync(session.Id, s.AsOtherReviewer));
        Assert.Null(await service.GetAsync(session.Id, candidate));
        Assert.Null(await service.ListAsync(Guid.NewGuid(), s.AsAdmin, new ReviewListQuery()));
    }

    [SkippableFact]
    public async Task Detail_has_timeline_contributions_score_history_and_signed_evidence_links()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        var session = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky,
            events: [new EventSpec(EventType.PHONE, 10, 8, WithEvidence: true), new EventSpec(EventType.TAB_BLUR, 2)]);
        var clock = Clock();
        var signer = new EvidenceLinkSigner(clock);

        var detail = await new ReviewService(db, Risk, signer, clock).GetAsync(session.Id, s.AsReviewer);

        Assert.NotNull(detail);
        Assert.Equal(ReviewStatus.AwaitingReview, detail.Status);
        Assert.Equal([EventType.TAB_BLUR, EventType.PHONE], detail.Events.Select(e => e.Type)); // zamana göre
        Assert.Null(detail.Events[0].EvidenceUrl);

        // Katkılar sınav bitişine göre; toplamları saklanan skorla aynı.
        Assert.Equal(session.RiskScore, detail.Events.Sum(e => e.Contribution), precision: 3);

        var frameId = await db.EvidenceFrames.Where(f => f.Event.ExamSessionId == session.Id).Select(f => f.Id).SingleAsync();
        var url = new Uri("http://x" + detail.Events[1].EvidenceUrl);
        var query = System.Web.HttpUtility.ParseQueryString(url.Query);
        Assert.Equal($"/api/evidence/{frameId}", url.AbsolutePath);
        Assert.True(signer.IsValid(frameId, long.Parse(query["exp"]!), query["sig"]));

        // Grafik: başlangıçta 0, olay anlarında nokta var, son nokta bitişteki skor.
        Assert.Equal(0, detail.ScoreHistory[0].Score);
        Assert.Contains(detail.ScoreHistory, p => p.At == ExamStart.AddMinutes(10));
        Assert.Equal(ExamStart.AddMinutes(30), detail.ScoreHistory[^1].At);
        Assert.Equal(session.RiskScore, detail.ScoreHistory[^1].Score, precision: 3);
        Assert.True(detail.ScoreHistory.Count <= 300 + 4);
    }

    [SkippableFact]
    public async Task Decision_is_only_for_flagged_completed_sessions_and_only_once()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        var flagged = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.PermissionGap);
        var clean = await SessionAsync(db, s.Exam, SessionState.Completed);
        var stuck = await SessionAsync(db, s.Exam, SessionState.AgentPairing);
        var service = Reviews(db, Clock());
        var request = new DecisionRequest(ReviewDecisionType.Suspicious, "  Telefon görüldü.  ");

        Assert.Equal(ReviewOperationStatus.Invalid, (await service.DecideAsync(flagged.Id, s.AsReviewer, new DecisionRequest(null, null))).Status);
        Assert.Equal(ReviewOperationStatus.Invalid,
            (await service.DecideAsync(flagged.Id, s.AsReviewer, new DecisionRequest(ReviewDecisionType.Clean, new string('x', 4001)))).Status);
        Assert.Equal(ReviewOperationStatus.NotFound, (await service.DecideAsync(flagged.Id, s.AsOtherReviewer, request)).Status);
        Assert.Equal(ReviewOperationStatus.NotFound, (await service.DecideAsync(flagged.Id, s.AsAdmin, request)).Status);
        Assert.Equal(ReviewOperationStatus.Conflict, (await service.DecideAsync(clean.Id, s.AsReviewer, request)).Status);
        Assert.Equal(ReviewOperationStatus.Conflict, (await service.DecideAsync(stuck.Id, s.AsReviewer, request)).Status);

        Assert.Equal(ReviewOperationStatus.Success, (await service.DecideAsync(flagged.Id, s.AsReviewer, request)).Status);
        Assert.Equal(ReviewOperationStatus.Conflict,
            (await service.DecideAsync(flagged.Id, s.AsReviewer, new DecisionRequest(ReviewDecisionType.Clean, null))).Status);

        db.ChangeTracker.Clear();
        var saved = await db.ExamSessions.Include(x => x.ReviewerDecision).SingleAsync(x => x.Id == flagged.Id);
        Assert.Equal(SessionState.Decided, saved.State);
        Assert.Equal(ReviewDecisionType.Suspicious, saved.ReviewerDecision!.Decision);
        Assert.Equal("Telefon görüldü.", saved.ReviewerDecision.Note);
        Assert.Equal(s.Reviewer.Id, saved.ReviewerDecision.ReviewerId);
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "review.decision" && a.TargetId == flagged.Id);
        Assert.Equal(s.Reviewer.Id, audit.ActorUserId);
        Assert.Contains("Suspicious", audit.NewValue);
        Assert.Equal(SessionState.Completed, (await db.ExamSessions.SingleAsync(x => x.Id == clean.Id)).State);
    }

    [SkippableFact]
    public async Task Notes_can_be_added_by_assigned_reviewer_before_and_after_decision()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        var session = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky);
        var service = Reviews(db, Clock());

        var (first, note) = await service.AddNoteAsync(session.Id, s.AsReviewer, new NoteRequest("İlk izlenim: telefon olabilir."));
        await service.DecideAsync(session.Id, s.AsReviewer, new DecisionRequest(ReviewDecisionType.Violation, null));
        var (second, _) = await service.AddNoteAsync(session.Id, s.AsReviewer, new NoteRequest("Karardan sonra ek not."));
        var (empty, _) = await service.AddNoteAsync(session.Id, s.AsReviewer, new NoteRequest("   "));
        var (asAdmin, _) = await service.AddNoteAsync(session.Id, s.AsAdmin, new NoteRequest("Admin notu"));
        var (asOther, _) = await service.AddNoteAsync(session.Id, s.AsOtherReviewer, new NoteRequest("Başkası"));

        Assert.Equal(ReviewOperationStatus.Success, first.Status);
        Assert.Equal(s.Reviewer.FullName, note!.AuthorName);
        Assert.Equal(ReviewOperationStatus.Success, second.Status);
        Assert.Equal(ReviewOperationStatus.Invalid, empty.Status);
        Assert.Equal(ReviewOperationStatus.NotFound, asAdmin.Status);
        Assert.Equal(ReviewOperationStatus.NotFound, asOther.Status);

        var detail = await service.GetAsync(session.Id, s.AsAdmin);
        Assert.Equal(["İlk izlenim: telefon olabilir.", "Karardan sonra ek not."], detail!.Notes.Select(n => n.Text));
        Assert.Equal(ReviewDecisionType.Violation, detail.Decision!.Decision);
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.Action == "review.note" && a.ActorUserId == s.Reviewer.Id));
    }

    [SkippableFact]
    public async Task False_alarm_keeps_the_event_removes_it_from_the_score_and_keeps_the_risky_flag()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        var session = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky,
            events: [new EventSpec(EventType.PHONE, 10, 8), new EventSpec(EventType.TAB_BLUR, 20)]);
        var phone = await db.Events.SingleAsync(e => e.ExamSessionId == session.Id && e.Type == EventType.PHONE);
        var blur = await db.Events.AsNoTracking().SingleAsync(e => e.ExamSessionId == session.Id && e.Type == EventType.TAB_BLUR);
        var clock = Clock();
        var service = Reviews(db, clock);

        Assert.Equal(ReviewOperationStatus.NotFound, (await service.MarkFalseAlarmAsync(phone.Id, s.AsOtherReviewer)).Status);
        Assert.Equal(ReviewOperationStatus.NotFound, (await service.MarkFalseAlarmAsync(Guid.NewGuid(), s.AsReviewer)).Status);
        Assert.Equal(ReviewOperationStatus.Success, (await service.MarkFalseAlarmAsync(phone.Id, s.AsReviewer)).Status);
        Assert.Equal(ReviewOperationStatus.Conflict, (await service.MarkFalseAlarmAsync(phone.Id, s.AsReviewer)).Status);

        db.ChangeTracker.Clear();
        var marked = await db.Events.SingleAsync(e => e.Id == phone.Id);
        Assert.True(marked.IsFalseAlarm);
        Assert.Equal(s.Reviewer.Id, marked.FalseAlarmMarkedById);
        Assert.Equal(clock.GetUtcNow(), marked.FalseAlarmMarkedAt);

        var onlyBlur = Risk.Calculate([new(blur.Id, blur.Type, blur.StartedAt, blur.DurationSec, blur.ReceivedAt)], ExamStart.AddMinutes(30));
        var saved = await db.ExamSessions.SingleAsync(x => x.Id == session.Id);
        Assert.Equal(onlyBlur.Score, saved.RiskScore);
        Assert.True(saved.Flags.HasFlag(SessionFlags.Risky)); // K-17: riskli işareti kalıcı
        Assert.Single(await db.AuditLogs.Where(a => a.Action == "event.false_alarm" && a.TargetId == phone.Id).ToListAsync());

        var detail = await service.GetAsync(session.Id, s.AsReviewer);
        var view = detail!.Events.Single(e => e.Id == phone.Id);
        Assert.True(view.IsFalseAlarm);
        Assert.Equal(0, view.Contribution);
        Assert.Equal(s.Reviewer.FullName, view.FalseAlarmMarkedBy);
    }

    [SkippableFact]
    public async Task False_alarm_is_rejected_after_the_decision()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        var session = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky, events: new EventSpec(EventType.PHONE, 10));
        var phone = await db.Events.AsNoTracking().SingleAsync(e => e.ExamSessionId == session.Id);
        var service = Reviews(db, Clock());

        await service.DecideAsync(session.Id, s.AsReviewer, new DecisionRequest(ReviewDecisionType.Violation, null));
        var result = await service.MarkFalseAlarmAsync(phone.Id, s.AsReviewer);

        Assert.Equal(ReviewOperationStatus.Conflict, result.Status);
        Assert.False((await db.Events.AsNoTracking().SingleAsync(e => e.Id == phone.Id)).IsFalseAlarm);
    }

    [SkippableFact]
    public async Task Report_counts_the_last_answer_per_question_and_summarises_risk()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        var q = Enumerable.Range(1, 4)
            .Select(i => new Question { ExamId = s.Exam.Id, Text = $"Soru {i}", Options = ["A", "B", "C"], CorrectOptionIndex = 1, Order = i })
            .ToList();
        db.Questions.AddRange(q);
        var session = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky, "Ayşe Aday",
            new EventSpec(EventType.PHONE, 10, 8), new EventSpec(EventType.PHONE, 12), new EventSpec(EventType.TAB_BLUR, 2));
        db.Answers.AddRange(
            new Answer { ExamSessionId = session.Id, QuestionId = q[0].Id, SelectedOption = 0, Seq = 1 },
            new Answer { ExamSessionId = session.Id, QuestionId = q[0].Id, SelectedOption = 1, Seq = 2 }, // son cevap doğru
            new Answer { ExamSessionId = session.Id, QuestionId = q[1].Id, SelectedOption = 2, Seq = 3, Unmonitored = true },
            new Answer { ExamSessionId = session.Id, QuestionId = q[2].Id, SelectedOption = 1, Seq = 4 },
            new Answer { ExamSessionId = session.Id, QuestionId = q[2].Id, SelectedOption = null, Seq = 5 }); // temizlendi
        await db.SaveChangesAsync();
        var stuck = await SessionAsync(db, s.Exam, SessionState.ConsentPending);
        var reports = Reports(db, Clock());

        var report = await reports.GetAsync(s.Exam.Id, session.Id, s.AsReviewer);

        Assert.NotNull(report);
        Assert.Equal("Ayşe Aday", report.CandidateName);
        Assert.Equal(new ExamResultView(QuestionCount: 4, Correct: 1, Wrong: 1, Blank: 2, Unmonitored: 1), report.Result);
        Assert.Equal(ReviewStatus.AwaitingReview, report.Status);
        Assert.Equal(EventType.PHONE, report.Events[0].Type);
        Assert.Equal(2, report.Events[0].Count);
        Assert.Equal(3, report.TopReasons.Count);
        Assert.StartsWith("10. dakikada PHONE", report.TopReasons[0]);

        // Hiç başlamayan oturumda sonuç yok; erişim kuralı inceleme uçlarıyla aynı.
        Assert.Null((await reports.GetAsync(s.Exam.Id, stuck.Id, s.AsAdmin))!.Result);
        Assert.Null(await reports.GetAsync(s.Exam.Id, session.Id, s.AsOtherReviewer));
        Assert.Null(await reports.GetAsync(Guid.NewGuid(), session.Id, s.AsAdmin));
    }

    [SkippableFact]
    public async Task Flagged_sessions_are_announced_when_submitted_or_auto_submitted()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var clock = new FakeTimeProvider(EventTestHelpers.Now.AddMinutes(10));
        var notifier = new RecordingReviewNotifier();
        var submitted = await EventTestHelpers.InProgressSessionAsync(db);
        var expired = await EventTestHelpers.InProgressSessionAsync(db);
        var expiredClean = await EventTestHelpers.InProgressSessionAsync(db);
        submitted.Flags = SessionFlags.NoAgent;
        expired.Flags = SessionFlags.Risky;
        await db.SaveChangesAsync();

        var sessions = new SessionService(db, EventTestHelpers.Service(db, clock), clock, notifier);
        await sessions.SubmitAsync(submitted.Id);
        clock.Advance(TimeSpan.FromMinutes(25)); // diğer ikisinin süresi doldu
        await new SessionTimekeeper(db, EventTestHelpers.Service(db, clock), clock, notifier).RunOnceAsync();

        Assert.Equal([submitted.Id, expired.Id], notifier.Messages.Select(m => m.SessionId));
        Assert.Equal([SessionFlags.NoAgent], notifier.Messages[0].Flags);
        Assert.Equal(EventTestHelpers.Now.AddMinutes(30), notifier.Messages[1].EndedAt);
        Assert.DoesNotContain(notifier.Messages, m => m.SessionId == expiredClean.Id);
    }

    [SkippableFact]
    public async Task Pre_start_session_is_listed_as_not_started_only_after_the_latest_start_time()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        var stuck = await SessionAsync(db, s.Exam, SessionState.ConsentPending);

        // Son başlangıç ExamStart + 30 dk: öncesinde aday hâlâ başlayabilir, listede yok.
        var early = await Reviews(db, new FakeTimeProvider(ExamStart.AddMinutes(10))).ListAsync(s.Exam.Id, s.AsReviewer, new ReviewListQuery());
        var late = await Reviews(db, Clock()).ListAsync(s.Exam.Id, s.AsReviewer, new ReviewListQuery());

        Assert.Empty(early!.Sessions);
        Assert.Equal(ReviewStatus.NotStarted, late!.Sessions.Single(r => r.SessionId == stuck.Id).Status);
        Assert.Null(await Reviews(db, new FakeTimeProvider(ExamStart.AddMinutes(10))).GetAsync(stuck.Id, s.AsReviewer));
    }

    [SkippableFact]
    public async Task Not_started_session_can_be_viewed_and_noted_but_not_decided_or_marked()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        var stuck = await SessionAsync(db, s.Exam, SessionState.IdentityVerification, events: new EventSpec(EventType.VERIFICATION_FAILED, -2, WithEvidence: true));
        var failed = await db.Events.AsNoTracking().SingleAsync(e => e.ExamSessionId == stuck.Id);

        // Saat günler sonrası: grafik yine de sınırlı sayıda nokta.
        var service = Reviews(db, new FakeTimeProvider(ExamStart.AddDays(5)));
        var detail = await service.GetAsync(stuck.Id, s.AsReviewer);
        var (note, _) = await service.AddNoteAsync(stuck.Id, s.AsReviewer, new NoteRequest("Aday destek talebinde bulundu."));

        Assert.Equal(ReviewStatus.NotStarted, detail!.Status);
        Assert.NotNull(detail.Events.Single().EvidenceUrl); // başarısız doğrulamanın kanıt karesi
        Assert.InRange(detail.ScoreHistory.Count, 2, 300 + 4);
        Assert.Equal(ReviewOperationStatus.Success, note.Status);
        Assert.Equal(ReviewOperationStatus.Conflict, (await service.MarkFalseAlarmAsync(failed.Id, s.AsReviewer)).Status);
        Assert.Equal(ReviewOperationStatus.Conflict,
            (await service.DecideAsync(stuck.Id, s.AsReviewer, new DecisionRequest(ReviewDecisionType.Clean, null))).Status);
    }

    [SkippableFact]
    public async Task Running_session_is_hidden_from_review()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        var running = await SessionAsync(db, s.Exam, SessionState.InProgress, SessionFlags.Risky, events: new EventSpec(EventType.PHONE, 5));
        var phone = await db.Events.AsNoTracking().SingleAsync(e => e.ExamSessionId == running.Id);
        var service = Reviews(db, Clock());

        Assert.Null(await service.GetAsync(running.Id, s.AsReviewer));
        Assert.Equal(ReviewOperationStatus.NotFound, (await service.AddNoteAsync(running.Id, s.AsReviewer, new NoteRequest("not"))).Result.Status);
        Assert.Equal(ReviewOperationStatus.Conflict, (await service.MarkFalseAlarmAsync(phone.Id, s.AsReviewer)).Status);
        Assert.Null(await Reports(db, Clock()).GetAsync(s.Exam.Id, running.Id, s.AsAdmin));
    }

    [SkippableFact]
    public async Task Note_longer_than_the_limit_is_rejected()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        var session = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky);
        var service = Reviews(db, Clock());

        var atLimit = await service.AddNoteAsync(session.Id, s.AsReviewer, new NoteRequest(new string('ş', ReviewerNote.MaxLength)));
        var overLimit = await service.AddNoteAsync(session.Id, s.AsReviewer, new NoteRequest(new string('ş', ReviewerNote.MaxLength + 1)));

        Assert.Equal(ReviewOperationStatus.Success, atLimit.Result.Status);
        Assert.Equal(ReviewOperationStatus.Invalid, overLimit.Result.Status);
    }

    [SkippableFact]
    public async Task List_can_be_sorted_by_start_time()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var s = await ScenarioAsync(db);
        var risky = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky);
        var stuck = await SessionAsync(db, s.Exam, SessionState.AgentPairing); // başlamadı: açıldığı an (ExamStart − 5 dk)

        var list = await Reviews(db, Clock()).ListAsync(s.Exam.Id, s.AsReviewer, new ReviewListQuery(Sort: ReviewSort.StartedAt));

        Assert.Equal([stuck.Id, risky.Id], list!.Sessions.Select(r => r.SessionId));
    }

    [SkippableFact]
    public async Task Two_reviewers_deciding_at_the_same_time_produce_exactly_one_decision()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        ReviewScenario s;
        ExamSession session;
        User second;
        await using (var db = fx.CreateContext())
        {
            s = await ScenarioAsync(db);
            session = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky);
            second = TestData.UserWithRole(UserRole.Reviewer);
            db.ExamAssignments.Add(new ExamAssignment { ExamId = s.Exam.Id, User = second, Role = AssignmentRole.Reviewer });
            await db.SaveChangesAsync();
        }

        // Her biri kendi bağlantısında: gerçek iki istek gibi.
        async Task<ReviewOperationResult> Decide(Guid reviewerId, ReviewDecisionType decision)
        {
            await using var db = fx.CreateContext();
            return await Reviews(db, Clock()).DecideAsync(session.Id, new ReviewActor(reviewerId, UserRole.Reviewer), new DecisionRequest(decision, null));
        }

        var results = await Task.WhenAll(
            Decide(s.Reviewer.Id, ReviewDecisionType.Clean),
            Decide(second.Id, ReviewDecisionType.Violation));

        Assert.Single(results, r => r.Status == ReviewOperationStatus.Success);
        Assert.Single(results, r => r.Status == ReviewOperationStatus.Conflict);
        await using var check = fx.CreateContext();
        Assert.Equal(1, await check.ReviewerDecisions.CountAsync(d => d.ExamSessionId == session.Id));
        Assert.Equal(1, await check.AuditLogs.CountAsync(a => a.Action == "review.decision" && a.TargetId == session.Id));
    }
}
