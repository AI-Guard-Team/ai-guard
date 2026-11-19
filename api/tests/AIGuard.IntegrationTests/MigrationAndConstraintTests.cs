using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AIGuard.IntegrationTests;

/// <summary>
/// Göçlerin boş veritabanına uygulanması ve kısıtların PostgreSQL'de gerçekten
/// çalıştığının kontrolü.
/// </summary>
public class MigrationAndConstraintTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private const string UniqueViolation = PostgresErrorCodes.UniqueViolation;   // 23505
    private const string CheckViolation = PostgresErrorCodes.CheckViolation;     // 23514

    private void RequireDb() => Skip.If(fx.ConnectionString is null, "ConnectionStrings__Default tanımlı değil; PostgreSQL testleri atlandı.");

    private static string SqlState(DbUpdateException ex) => Assert.IsType<PostgresException>(ex.InnerException).SqlState;

    [SkippableFact]
    public async Task All_migrations_are_applied_to_empty_database()
    {
        RequireDb();
        await using var db = fx.CreateContext();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
    }

    [SkippableFact]
    public async Task Candidate_cannot_have_two_sessions_for_same_exam()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var first = await TestData.SavedSessionAsync(db);

        db.ExamSessions.Add(new ExamSession { ExamId = first.ExamId, CandidateId = first.CandidateId });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(UniqueViolation, SqlState(ex));
    }

    [SkippableFact]
    public async Task Cancelled_session_does_not_use_up_the_attempt()
    {
        // FR-10: rızayı reddeden adayın oturumu iptal olur, hakkı kullanılmış sayılmaz.
        RequireDb();
        await using var db = fx.CreateContext();
        var first = await TestData.SavedSessionAsync(db);
        first.State = SessionState.Cancelled;
        await db.SaveChangesAsync();

        db.ExamSessions.Add(new ExamSession { ExamId = first.ExamId, CandidateId = first.CandidateId });
        await db.SaveChangesAsync();

        db.ExamSessions.Add(new ExamSession { ExamId = first.ExamId, CandidateId = first.CandidateId });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(UniqueViolation, SqlState(ex));
    }

    [SkippableFact]
    public async Task Replayed_agent_nonce_is_rejected_by_database()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await TestData.SavedSessionAsync(db);
        var pairing = new AgentPairing { ExamSessionId = session.Id, PairingCodeHash = Guid.NewGuid().ToString("N") };
        pairing.Signals.Add(new AgentSignal { Nonce = "n-1", SignatureValid = true });
        db.AgentPairings.Add(pairing);
        await db.SaveChangesAsync();

        db.AgentSignals.Add(new AgentSignal { AgentPairingId = pairing.Id, Nonce = "n-1", SignatureValid = true });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(UniqueViolation, SqlState(ex));
    }

    [SkippableFact]
    public async Task Same_answer_seq_cannot_be_stored_twice()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await TestData.SavedSessionAsync(db);
        var question = new Question { ExamId = session.ExamId, Text = "S", Options = ["a", "b"], CorrectOptionIndex = 0, Order = 1 };
        db.Questions.Add(question);
        db.Answers.Add(new Answer { ExamSessionId = session.Id, QuestionId = question.Id, SelectedOption = 0, Seq = 1 });
        await db.SaveChangesAsync();

        db.Answers.Add(new Answer { ExamSessionId = session.Id, QuestionId = question.Id, SelectedOption = 1, Seq = 1 });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(UniqueViolation, SqlState(ex));
    }

    [SkippableFact]
    public async Task Duplicate_normalized_email_is_rejected()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var email = $"{Guid.NewGuid():N}@test.local";
        db.Users.Add(TestData.Candidate(email));
        await db.SaveChangesAsync();

        db.Users.Add(TestData.Candidate(email.ToUpperInvariant()));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(UniqueViolation, SqlState(ex));
    }

    [SkippableTheory]
    [InlineData(new[] { "tek" }, 0)]   // en az 2 seçenek
    [InlineData(new[] { "a", "b" }, 2)] // doğru seçenek aralık dışı
    [InlineData(new[] { "a", "b" }, -1)]
    public async Task Invalid_question_is_rejected_by_check_constraint(string[] options, int correct)
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var exam = TestData.Exam();
        exam.Questions.Add(new Question { Text = "S", Options = [.. options], CorrectOptionIndex = correct, Order = 1 });
        db.Exams.Add(exam);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(CheckViolation, SqlState(ex));
    }

    [SkippableFact]
    public async Task Exam_window_must_end_after_start()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var exam = TestData.Exam();
        exam.WindowEndAt = exam.WindowStartAt.AddMinutes(-1);
        db.Exams.Add(exam);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(CheckViolation, SqlState(ex));
    }

    [SkippableFact]
    public async Task Session_consent_without_session_is_rejected()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        db.ConsentRecords.Add(new ConsentRecord { User = TestData.Candidate(), Type = ConsentType.Session, TextVersion = "v1" });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(CheckViolation, SqlState(ex));
    }

    [SkippableFact]
    public async Task Oversized_face_template_is_rejected()
    {
        // Şablona yanlışlıkla fotoğraf gibi büyük bir veri yazılamaz.
        RequireDb();
        await using var db = fx.CreateContext();
        var user = TestData.Candidate();
        var consent = new ConsentRecord { User = user, Type = ConsentType.Biometric, TextVersion = "v1" };
        db.ReferenceFaceTemplates.Add(new ReferenceFaceTemplate { User = user, ConsentRecord = consent, EncryptedVector = new byte[200_000] });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(CheckViolation, SqlState(ex));
    }

    [SkippableFact]
    public async Task False_alarm_requires_marker_and_time()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await TestData.SavedSessionAsync(db);
        db.Events.Add(new Event
        {
            ExamSessionId = session.Id,
            Type = EventType.PHONE,
            Source = EventSource.Camera,
            StartedAt = DateTimeOffset.UtcNow,
            IsFalseAlarm = true, // işaretleyen yok
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(CheckViolation, SqlState(ex));
    }

    [SkippableFact]
    public async Task Audit_log_can_be_inserted_but_not_updated_or_deleted()
    {
        RequireDb();
        await using (var db = fx.CreateContext())
        {
            db.AuditLogs.Add(new AuditLog { Action = "test.insert", NewValue = """{"a":1}""" });
            await db.SaveChangesAsync();
        }

        await using (var db = fx.CreateContext())
        {
            var log = await db.AuditLogs.FirstAsync(a => a.Action == "test.insert");
            db.AuditLogs.Remove(log);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }

        await using (var db = fx.CreateContext())
        {
            Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "test.insert"));
        }
    }

    [SkippableFact]
    public async Task Concurrent_session_updates_are_detected()
    {
        // Otomatik teslim görevi ile adayın teslim isteği aynı anda gelirse biri hata almalı.
        RequireDb();
        Guid id;
        await using (var setup = fx.CreateContext())
        {
            id = (await TestData.SavedSessionAsync(setup)).Id;
        }

        await using var a = fx.CreateContext();
        await using var b = fx.CreateContext();
        var sa = await a.ExamSessions.SingleAsync(s => s.Id == id);
        var sb = await b.ExamSessions.SingleAsync(s => s.Id == id);

        sa.State = SessionState.Completed;
        await a.SaveChangesAsync();

        sb.State = SessionState.Cancelled;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
    }

    [SkippableFact]
    public async Task Enums_are_persisted_as_text()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await TestData.SavedSessionAsync(db);

        var state = await db.Database
            .SqlQuery<string>($"SELECT state AS \"Value\" FROM exam_sessions WHERE id = {session.Id}")
            .SingleAsync();

        Assert.Equal("ConsentPending", state);
    }
}
