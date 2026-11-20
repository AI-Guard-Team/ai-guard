using AIGuard.Application.Exams;
using AIGuard.Application.Sessions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Domain.Sessions;
using AIGuard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.Sessions;

/// <summary>Oturum akışı kuralları; saat FakeTimeProvider ile ileri alınıyor (K-15).</summary>
public class SessionServiceTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private readonly FakeTimeProvider _clock = Clock(); // EventTestHelpers.Now

    private SessionService Service(AppDbContext db) => new(db, Events.EventTestHelpers.Service(db, _clock), _clock);

    /// <summary>Pencere Now − 1 sa … Now + 1 gün, 30 dk sınav, 2 soru; aday atanmış.</summary>
    private async Task<(Exam Exam, User Candidate, Question[] Questions)> ExamAsync()
    {
        await using var db = fx.CreateContext();
        var candidate = TestData.Candidate();
        var exam = new Exam
        {
            Name = "Oturum testi", Type = ExamType.GeneralAptitude, DurationMinutes = 30,
            WindowStartAt = Now.AddHours(-1), WindowEndAt = Now.AddDays(1),
            Questions =
            {
                new Question { Order = 1, Text = "S1", Options = ["a", "b", "c"], CorrectOptionIndex = 0 },
                new Question { Order = 2, Text = "S2", Options = ["a", "b"], CorrectOptionIndex = 1 },
            },
        };
        exam.Assignments.Add(new ExamAssignment { User = candidate, Role = AssignmentRole.Candidate });
        db.Exams.Add(exam);
        await db.SaveChangesAsync();
        return (exam, candidate, exam.Questions.OrderBy(q => q.Order).ToArray());
    }

    private async Task<Guid> InProgressAsync(Exam exam, User candidate)
    {
        await using var db = fx.CreateContext();
        var started = await Service(db).StartAsync(exam.Id, candidate.Id);
        // Rıza ve ajan eşleştirme (A3, Y1) burada atlanıyor; durum doğrudan doğrulama adımına alınıyor.
        var session = await db.ExamSessions.SingleAsync(s => s.Id == started.SessionId);
        session.State = SessionState.IdentityVerification;
        await db.SaveChangesAsync();
        Assert.Equal(SessionOperationStatus.Success, (await Service(db).BeginExamAsync(session.Id)).Status);
        return session.Id;
    }

    private async Task<ExamSession> ReloadAsync(Guid id)
    {
        await using var db = fx.CreateContext();
        return await db.ExamSessions.AsNoTracking().SingleAsync(s => s.Id == id);
    }

    [SkippableFact]
    public async Task Start_creates_a_consent_pending_session_and_returns_the_same_one_again()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (exam, candidate, _) = await ExamAsync();
        await using var db = fx.CreateContext();

        var first = await Service(db).StartAsync(exam.Id, candidate.Id);
        var again = await Service(db).StartAsync(exam.Id, candidate.Id);

        Assert.Equal((StartSessionStatus.Created, SessionState.ConsentPending), (first.Status, first.State!.Value));
        Assert.Equal(StartSessionStatus.Resumed, again.Status);
        Assert.Equal(first.SessionId, again.SessionId);
    }

    [SkippableFact]
    public async Task Start_checks_assignment_window_and_attempt()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (exam, candidate, _) = await ExamAsync();
        var stranger = TestData.Candidate();
        await using var db = fx.CreateContext();
        db.Users.Add(stranger);
        await db.SaveChangesAsync();

        var notAssigned = await Service(db).StartAsync(exam.Id, stranger.Id);
        _clock.Advance(TimeSpan.FromDays(1)); // son başlangıç (bitiş − 30 dk) geçti
        var late = await Service(db).StartAsync(exam.Id, candidate.Id);

        Assert.Equal(StartSessionStatus.NotFound, notAssigned.Status);
        Assert.Equal((StartSessionStatus.Denied, StartDenial.TooLate), (late.Status, late.Denial));
    }

    [SkippableFact]
    public async Task Begin_starts_the_clock_and_uses_the_single_attempt()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (exam, candidate, _) = await ExamAsync();

        var id = await InProgressAsync(exam, candidate);
        var session = await ReloadAsync(id);
        await using var db = fx.CreateContext();
        var assignment = await db.ExamAssignments.SingleAsync(a => a.ExamId == exam.Id && a.UserId == candidate.Id);

        Assert.Equal(SessionState.InProgress, session.State);
        Assert.Equal(Now, session.StartedAt);
        Assert.Equal(Now.AddMinutes(30), session.EndsAt); // sunucu saatine göre
        Assert.Equal(Now, assignment.AttemptUsedAt);
    }

    [SkippableFact]
    public async Task Consent_refusal_does_not_use_the_attempt()
    {
        // FR-10: rıza reddedilip iptal edilen oturum hakkı tüketmiyor; aday yeniden başlayabiliyor.
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (exam, candidate, _) = await ExamAsync();
        await using var db = fx.CreateContext();
        var first = await Service(db).StartAsync(exam.Id, candidate.Id);
        var session = await db.ExamSessions.SingleAsync(s => s.Id == first.SessionId);
        session.TransitionTo(SessionState.Cancelled);
        await db.SaveChangesAsync();

        var second = await Service(db).StartAsync(exam.Id, candidate.Id);

        Assert.Equal(StartSessionStatus.Created, second.Status);
        Assert.NotEqual(first.SessionId, second.SessionId);
    }

    [SkippableFact]
    public async Task Highest_seq_wins_and_duplicate_seq_is_ignored()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (exam, candidate, q) = await ExamAsync();
        var id = await InProgressAsync(exam, candidate);
        await using var db = fx.CreateContext();
        var service = Service(db);

        await service.SaveAnswerAsync(id, new AnswerRequest(q[0].Id, 2, 2, null));
        // Çevrimdışı kuyruktan eski cevap geç geldi: daha düşük seq, geçerli cevabı değiştirmemeli.
        await service.SaveAnswerAsync(id, new AnswerRequest(q[0].Id, 0, 1, null));
        var duplicate = await service.SaveAnswerAsync(id, new AnswerRequest(q[0].Id, 2, 2, null));
        var reused = await service.SaveAnswerAsync(id, new AnswerRequest(q[1].Id, 1, 2, null));
        var view = await service.GetAsync(id);

        Assert.Equal(SessionOperationStatus.Duplicate, duplicate.Status);
        Assert.Equal(SessionOperationStatus.Conflict, reused.Status); // aynı seq başka cevap için
        Assert.Equal(2, view!.Answers.Single(a => a.QuestionId == q[0].Id).SelectedOption);
        Assert.Equal(2, view.LastSeq);
        Assert.Equal(2, await db.Answers.CountAsync(a => a.ExamSessionId == id));
    }

    [SkippableFact]
    public async Task Answers_are_validated_against_the_exam()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (exam, candidate, q) = await ExamAsync();
        var (otherExam, _, otherQ) = await ExamAsync();
        var id = await InProgressAsync(exam, candidate);
        await using var db = fx.CreateContext();
        var service = Service(db);

        var foreign = await service.SaveAnswerAsync(id, new AnswerRequest(otherQ[0].Id, 0, 1, null)); // S-6
        var outOfRange = await service.SaveAnswerAsync(id, new AnswerRequest(q[1].Id, 2, 1, null));
        var noSeq = await service.SaveAnswerAsync(id, new AnswerRequest(q[0].Id, 0, 0, null));
        var cleared = await service.SaveAnswerAsync(id, new AnswerRequest(q[0].Id, null, 1, null)); // cevap temizlendi

        Assert.Equal(SessionOperationStatus.Invalid, foreign.Status);
        Assert.Equal(SessionOperationStatus.Invalid, outOfRange.Status);
        Assert.Equal(SessionOperationStatus.Invalid, noSeq.Status);
        Assert.Equal(SessionOperationStatus.Success, cleared.Status);
        Assert.NotEqual(exam.Id, otherExam.Id);
    }

    [SkippableFact]
    public async Task Answers_after_time_are_rejected_and_timekeeper_auto_submits()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (exam, candidate, q) = await ExamAsync();
        var id = await InProgressAsync(exam, candidate);
        await using var db = fx.CreateContext();

        _clock.Advance(TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(1));
        // Otomatik teslim görevi henüz çalışmadı; yine de sunucu saatine göre reddediliyor.
        var late = await Service(db).SaveAnswerAsync(id, new AnswerRequest(q[0].Id, 0, 1, null));
        var (completed, _) = await new SessionTimekeeper(db, _clock).RunOnceAsync();
        var session = await ReloadAsync(id);

        Assert.Equal(SessionOperationStatus.Conflict, late.Status);
        Assert.True(completed >= 1);
        Assert.Equal(SessionState.Completed, session.State);
        Assert.Equal(session.EndsAt, session.EndedAt); // bitiş anı süre sonu
    }

    [SkippableFact]
    public async Task Submit_completes_once()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (exam, candidate, _) = await ExamAsync();
        var id = await InProgressAsync(exam, candidate);
        await using var db = fx.CreateContext();

        _clock.Advance(TimeSpan.FromMinutes(10));
        var first = await Service(db).SubmitAsync(id);
        var second = await Service(db).SubmitAsync(id);
        var session = await ReloadAsync(id);

        Assert.Equal(SessionOperationStatus.Success, first.Status);
        Assert.Equal(SessionOperationStatus.Conflict, second.Status);
        Assert.Equal((SessionState.Completed, Now.AddMinutes(10)), (session.State, session.EndedAt!.Value));
    }

    [SkippableFact]
    public async Task Idle_session_is_disconnected_answers_are_unmonitored_and_return_records_the_gap()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (exam, candidate, q) = await ExamAsync();
        var id = await InProgressAsync(exam, candidate);
        await using var db = fx.CreateContext();

        _clock.Advance(SessionTimekeeper.DisconnectAfter + TimeSpan.FromSeconds(5));
        var (_, disconnected) = await new SessionTimekeeper(db, _clock).RunOnceAsync();
        // Dönüşte kuyruktaki cevap, yeniden doğrulamadan önce gönderiliyor.
        await Service(db).SaveAnswerAsync(id, new AnswerRequest(q[0].Id, 1, 1, null));
        _clock.Advance(TimeSpan.FromSeconds(20));
        var resumed = await Service(db).ResumeAfterReconnectAsync(id);

        var answer = await db.Answers.AsNoTracking().SingleAsync(a => a.ExamSessionId == id);
        var gap = await db.Events.AsNoTracking().SingleAsync(e => e.ExamSessionId == id && e.Type == EventType.CONNECTION_LOST);
        var session = await ReloadAsync(id);

        Assert.True(disconnected >= 1);
        Assert.True(answer.Unmonitored); // FR-09: izlenmeyen sürede verildi
        Assert.Equal(SessionOperationStatus.Success, resumed.Status);
        Assert.Equal(SessionState.InProgress, session.State);
        Assert.Equal(Now, gap.StartedAt); // son görülme anından
        Assert.Equal(55, gap.DurationSec, precision: 0);
    }

    [SkippableFact]
    public async Task Undefined_transition_throws()
    {
        // Kart: tanımsız geçiş reddedilir. Başlamamış oturumda doğrulama sonrası sınav başlatılamaz.
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (exam, candidate, _) = await ExamAsync();
        await using var db = fx.CreateContext();
        var started = await Service(db).StartAsync(exam.Id, candidate.Id);

        await Assert.ThrowsAsync<InvalidSessionTransitionException>(() => Service(db).BeginExamAsync(started.SessionId!.Value));
        Assert.Equal(SessionOperationStatus.Conflict, (await Service(db).SubmitAsync(started.SessionId!.Value)).Status);
    }

    [SkippableFact]
    public async Task Questions_are_hidden_while_permission_is_pending()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (exam, candidate, q) = await ExamAsync();
        var id = await InProgressAsync(exam, candidate);
        await using var db = fx.CreateContext();

        var running = await Service(db).GetAsync(id);
        var session = await db.ExamSessions.SingleAsync(s => s.Id == id);
        session.TransitionTo(SessionState.PermissionPending);
        await db.SaveChangesAsync();
        var hidden = await Service(db).GetAsync(id);
        var answer = await Service(db).SaveAnswerAsync(id, new AnswerRequest(q[0].Id, 0, 1, null));

        Assert.Equal(2, running!.Questions.Count);
        Assert.Empty(hidden!.Questions); // FR-17
        Assert.Equal(SessionOperationStatus.Conflict, answer.Status);
    }
}
