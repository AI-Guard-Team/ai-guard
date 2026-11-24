using System.Text.Json;
using AIGuard.Application.DataProtection;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Persistence;
using AIGuard.IntegrationTests.Biometrics;
using AIGuard.IntegrationTests.Review;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using static AIGuard.IntegrationTests.Review.ReviewTestHelpers;

namespace AIGuard.IntegrationTests.DataProtection;

/// <summary>
/// A5: saklama süresi dolan verilerin silinmesi (FR-35, GAB 3.3.8 veri yaşam döngüsü, K-32). Sınavlar
/// <see cref="ExamStart"/>'ta başlayıp 30 dakika sürüyor; saklama süresi varsayılan 30 gün.
/// </summary>
/// <remarks>
/// Aynı sınıftaki testler aynı veritabanını paylaşıyor; bu yüzden toplam sayılar yerine testin kendi
/// kayıtlarının durumu kontrol ediliyor.
/// </remarks>
public class DataRetentionServiceTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private static readonly byte[] Image = [0xFF, 0xD8, 0xFF, 0xE0];

    [SkippableFact]
    public async Task Expired_evidence_frames_are_deleted_from_disk_and_database()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var store = new MemoryEvidenceStore();
        var scenario = await ScenarioAsync(db);
        var session = await SessionAsync(db, scenario.Exam, SessionState.Completed, SessionFlags.Risky, "Aday",
            new EventSpec(EventType.PHONE, 5, 10, WithEvidence: true), new EventSpec(EventType.PHONE, 15, 10, WithEvidence: true));
        var frames = await FramesAsync(db, session.Id, store);
        frames[0].DeleteAfter = ExamStart.AddDays(1);
        await db.SaveChangesAsync();

        await Service(db, store, ExamStart.AddDays(2)).RunOnceAsync();

        db.ChangeTracker.Clear();
        var left = await db.EvidenceFrames.Where(f => f.Event.ExamSessionId == session.Id).Select(f => f.Id).ToListAsync();
        Assert.Equal([frames[1].Id], left);
        Assert.False(store.Files.ContainsKey(frames[0].StorageKey));
        Assert.True(store.Files.ContainsKey(frames[1].StorageKey));

        // Olay kalıyor; yalnızca karesi gitti. Oturumun saklama süresi henüz dolmadı.
        Assert.Equal(2, await db.Events.CountAsync(e => e.ExamSessionId == session.Id));
        Assert.Null((await db.ExamSessions.SingleAsync(s => s.Id == session.Id)).MonitoringDataDeletedAt);
    }

    [SkippableFact]
    public async Task Monitoring_data_is_deleted_when_retention_ends_but_result_and_decision_stay()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var store = new MemoryEvidenceStore();
        var scenario = await ScenarioAsync(db);
        var session = await SessionAsync(db, scenario.Exam, SessionState.Decided, SessionFlags.Risky, "Aday",
            new EventSpec(EventType.PHONE, 5, 10, WithEvidence: true), new EventSpec(EventType.TAB_BLUR, 8, 20));
        var frames = await FramesAsync(db, session.Id, store);

        // Kare normalde kendi DeleteAfter'ında, oturumdan önce siliniyor (ilk test). Burada sonraya alınıyor (ör. sınavın
        // saklama süresi sonradan kısaltıldı): oturum temizliği kareyi ve dosyasını da almalı.
        frames[0].DeleteAfter = ExamStart.AddDays(60);
        await AddReviewAndMonitoringRecordsAsync(db, session, scenario.Reviewer);
        var score = session.RiskScore;

        // Sınav ExamStart + 30 dk'da bitti; 30 günlük süre ExamStart + 30 gün + 30 dk'da doluyor.
        await Service(db, store, ExamStart.AddDays(30)).RunOnceAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(2, await db.Events.CountAsync(e => e.ExamSessionId == session.Id));

        var now = ExamStart.AddDays(31);
        await Service(db, store, now).RunOnceAsync();

        db.ChangeTracker.Clear();
        Assert.Equal(0, await db.Events.CountAsync(e => e.ExamSessionId == session.Id));
        Assert.Equal(0, await db.EvidenceFrames.CountAsync(f => frames.Select(x => x.Id).Contains(f.Id)));
        Assert.All(frames, f => Assert.False(store.Files.ContainsKey(f.StorageKey)));
        Assert.Equal(0, await db.AgentSignals.CountAsync(s => s.AgentPairing.ExamSessionId == session.Id));
        Assert.Equal(0, await db.Verifications.CountAsync(v => v.ExamSessionId == session.Id));

        // Sınav sonucu, skor, karar ve notlar kalıyor.
        var after = await db.ExamSessions.SingleAsync(s => s.Id == session.Id);
        Assert.Equal(now, after.MonitoringDataDeletedAt);
        Assert.Equal(score, after.RiskScore);
        Assert.Equal(1, await db.Answers.CountAsync(a => a.ExamSessionId == session.Id));
        Assert.True(await db.ReviewerDecisions.AnyAsync(d => d.ExamSessionId == session.Id));
        Assert.True(await db.ReviewerNotes.AnyAsync(n => n.ExamSessionId == session.Id));

        // Silme denetim kaydında, kaç kaydın gittiğiyle.
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "retention.session_purged" && a.TargetId == session.Id);
        using var details = JsonDocument.Parse(audit.NewValue!);
        Assert.Equal(2, details.RootElement.GetProperty("events").GetInt32());
        Assert.Equal(1, details.RootElement.GetProperty("evidenceFrames").GetInt32());
        Assert.Equal(1, details.RootElement.GetProperty("agentSignals").GetInt32());
        Assert.Equal(1, details.RootElement.GetProperty("verifications").GetInt32());

        // İkinci tur aynı oturumu yeniden işlemiyor.
        await Service(db, store, now.AddDays(1)).RunOnceAsync();
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "retention.session_purged" && a.TargetId == session.Id));
    }

    [SkippableFact]
    public async Task Retention_uses_the_exam_setting_skips_running_sessions_and_counts_not_started_from_the_window_end()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var store = new MemoryEvidenceStore();
        var scenario = await ScenarioAsync(db);
        scenario.Exam.RetentionDays = 7;
        await db.SaveChangesAsync();

        var completed = await SessionAsync(db, scenario.Exam, SessionState.Completed, SessionFlags.Risky, "Biten",
            new EventSpec(EventType.PHONE, 5, 10));
        var running = await SessionAsync(db, scenario.Exam, SessionState.InProgress, SessionFlags.None, "Süren",
            new EventSpec(EventType.PHONE, 5, 10));

        // Hiç başlamayan oturum: süre, sınav penceresinin kapandığı andan (ExamStart + 1 saat) sayılıyor.
        var notStarted = await SessionAsync(db, scenario.Exam, SessionState.IdentityVerification, SessionFlags.None, "Başlamayan",
            new EventSpec(EventType.VERIFICATION_FAILED, -2));

        await Service(db, store, ExamStart.AddDays(7)).RunOnceAsync();
        db.ChangeTracker.Clear();
        Assert.Null((await db.ExamSessions.SingleAsync(s => s.Id == completed.Id)).MonitoringDataDeletedAt);

        await Service(db, store, ExamStart.AddDays(7).AddHours(2)).RunOnceAsync();
        db.ChangeTracker.Clear();
        Assert.NotNull((await db.ExamSessions.SingleAsync(s => s.Id == completed.Id)).MonitoringDataDeletedAt);
        Assert.NotNull((await db.ExamSessions.SingleAsync(s => s.Id == notStarted.Id)).MonitoringDataDeletedAt);
        Assert.Null((await db.ExamSessions.SingleAsync(s => s.Id == running.Id)).MonitoringDataDeletedAt);
        Assert.Equal(1, await db.Events.CountAsync(e => e.ExamSessionId == running.Id));
    }

    [SkippableFact]
    public async Task Face_template_is_deleted_30_days_after_the_last_exam()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var store = new MemoryEvidenceStore();
        var scenario = await ScenarioAsync(db);

        // Sınava girmiş aday: son sınav ExamStart + 30 dk'da bitti.
        var examined = await SessionAsync(db, scenario.Exam, SessionState.Completed);
        await TemplateAsync(db, examined.CandidateId, createdAt: ExamStart.AddDays(-10));

        // Hiç sınava girmemiş iki aday: biri eski kayıtlı, biri yeni.
        var oldUser = TestData.Candidate();
        var newUser = TestData.Candidate();
        db.Users.AddRange(oldUser, newUser);
        await db.SaveChangesAsync();
        await TemplateAsync(db, oldUser.Id, createdAt: ExamStart.AddDays(-40));
        await TemplateAsync(db, newUser.Id, createdAt: ExamStart.AddDays(25));

        await Service(db, store, ExamStart.AddDays(29)).RunOnceAsync();
        db.ChangeTracker.Clear();
        Assert.True(await HasTemplateAsync(db, examined.CandidateId));
        Assert.False(await HasTemplateAsync(db, oldUser.Id));

        var now = ExamStart.AddDays(31);
        await Service(db, store, now).RunOnceAsync();
        db.ChangeTracker.Clear();
        Assert.False(await HasTemplateAsync(db, examined.CandidateId));
        Assert.True(await HasTemplateAsync(db, newUser.Id));

        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "retention.face_template_deleted" && a.TargetId == examined.CandidateId);
        Assert.Equal(now, audit.OccurredAt);
        Assert.Null(audit.ActorUserId);

        // Rıza kaydı ispat olarak kalıyor (1 yıl dolmadı).
        Assert.True(await db.ConsentRecords.AnyAsync(c => c.UserId == examined.CandidateId));
    }

    [SkippableFact]
    public async Task Consent_records_are_deleted_after_a_year_unless_a_face_template_still_uses_them()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var store = new MemoryEvidenceStore();
        var now = ExamStart.AddDays(400);

        var user = TestData.Candidate();
        db.Users.Add(user);
        var withdrawn = Consent(user, grantedAt: ExamStart.AddDays(-10), withdrawnAt: ExamStart.AddDays(-5));
        var recent = Consent(user, grantedAt: now.AddDays(-10));
        var oldButInUse = Consent(user, grantedAt: ExamStart);
        db.ConsentRecords.AddRange(withdrawn, recent, oldButInUse);
        db.ReferenceFaceTemplates.Add(new ReferenceFaceTemplate
        {
            UserId = user.Id, EncryptedVector = [1, 2, 3], ConsentRecord = oldButInUse, CreatedAt = now.AddDays(-1),
        });
        await db.SaveChangesAsync();

        await Service(db, store, now).RunOnceAsync();

        db.ChangeTracker.Clear();
        var left = await db.ConsentRecords.Where(c => c.UserId == user.Id).Select(c => c.Id).ToListAsync();
        Assert.DoesNotContain(withdrawn.Id, left);
        Assert.Contains(recent.Id, left);
        Assert.Contains(oldButInUse.Id, left);
    }

    [SkippableFact]
    public async Task Audit_logs_older_than_a_year_are_deleted_and_the_run_is_audited()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();

        // Veritabanı tetikleyicisi gerçek saate (now()) bakıyor; bu test de gerçek saatle çalışıyor.
        var now = DateTimeOffset.UtcNow;
        var old = new AuditLog { Action = "test.old", OccurredAt = now.AddDays(-800) };
        var recent = new AuditLog { Action = "test.recent", OccurredAt = now.AddDays(-300) };
        db.AuditLogs.AddRange(old, recent);
        await db.SaveChangesAsync();

        var result = await Service(db, new MemoryEvidenceStore(), now).RunOnceAsync();

        db.ChangeTracker.Clear();
        Assert.False(await db.AuditLogs.AnyAsync(a => a.Id == old.Id));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Id == recent.Id));
        Assert.True(result.AuditLogs >= 1);

        var summary = await db.AuditLogs.SingleAsync(a => a.Action == "retention.cleanup" && a.OccurredAt == now);
        using var details = JsonDocument.Parse(summary.NewValue!);
        Assert.Equal(result.AuditLogs, details.RootElement.GetProperty("AuditLogs").GetInt32());
    }

    [SkippableFact]
    public async Task A_run_with_nothing_to_delete_changes_nothing_and_writes_no_audit_entry()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var scenario = await ScenarioAsync(db);
        await SessionAsync(db, scenario.Exam, SessionState.Completed, SessionFlags.Risky, "Aday", new EventSpec(EventType.PHONE, 5, 10));

        // Bütün test verisinden önceki bir an: hiçbir kaydın süresi dolmamış.
        var now = ExamStart.AddDays(-1000);
        var result = await Service(db, new MemoryEvidenceStore(), now).RunOnceAsync();

        Assert.False(result.Any);
        Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "retention.cleanup" && a.OccurredAt == now));
    }

    [SkippableFact]
    public async Task Review_detail_and_report_say_that_monitoring_data_was_deleted()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var scenario = await ScenarioAsync(db);
        var session = await SessionAsync(db, scenario.Exam, SessionState.Completed, SessionFlags.Risky, "Aday",
            new EventSpec(EventType.PHONE, 5, 10), new EventSpec(EventType.MULTIPLE_FACES, 12, 6));
        var now = ExamStart.AddDays(31);
        await Service(db, new MemoryEvidenceStore(), now).RunOnceAsync();
        db.ChangeTracker.Clear();

        var clock = new FakeTimeProvider(now.AddHours(1));
        var detail = await Reviews(db, clock).GetAsync(session.Id, scenario.AsReviewer);
        Assert.NotNull(detail);
        Assert.Equal(now, detail.MonitoringDataDeletedAt);
        Assert.Empty(detail.Events);
        Assert.Empty(detail.ScoreHistory);
        Assert.Equal(session.RiskScore, detail.RiskScore);

        var report = await Reports(db, clock).GetAsync(scenario.Exam.Id, session.Id, scenario.AsReviewer);
        Assert.NotNull(report);
        Assert.Equal(now, report.MonitoringDataDeletedAt);
        Assert.NotNull(report.Result);
        Assert.Empty(report.TopReasons);
        Assert.Equal(session.RiskScore, report.RiskScore);
    }

    private static DataRetentionService Service(AppDbContext db, MemoryEvidenceStore store, DateTimeOffset now) =>
        new(db, store, new FakeTimeProvider(now));

    /// <summary>Oturumun kanıt kareleri; dosyaları bellek deposuna konuyor.</summary>
    private static async Task<List<EvidenceFrame>> FramesAsync(AppDbContext db, Guid sessionId, MemoryEvidenceStore store)
    {
        var frames = await db.EvidenceFrames.Where(f => f.Event.ExamSessionId == sessionId).OrderBy(f => f.Event.StartedAt).ToListAsync();
        foreach (var frame in frames)
        {
            store.Files[frame.StorageKey] = Image;
        }

        return frames;
    }

    /// <summary>Kalması gerekenler (cevap, karar, not) ve silinmesi gerekenler (ajan sinyali, doğrulama).</summary>
    private static async Task AddReviewAndMonitoringRecordsAsync(AppDbContext db, ExamSession session, User reviewer)
    {
        var question = new Question { ExamId = session.ExamId, Text = "Soru", Options = ["A", "B"], CorrectOptionIndex = 0 };
        db.Questions.Add(question);
        db.Answers.Add(new Answer { ExamSessionId = session.Id, Question = question, SelectedOption = 0, Seq = 1, ReceivedAt = ExamStart.AddMinutes(3) });
        db.ReviewerDecisions.Add(new ReviewerDecision { ExamSessionId = session.Id, ReviewerId = reviewer.Id, Decision = ReviewDecisionType.Suspicious });
        db.ReviewerNotes.Add(new ReviewerNote { ExamSessionId = session.Id, AuthorId = reviewer.Id, Text = "Not" });

        var pairing = new AgentPairing { ExamSessionId = session.Id, PairingCodeHash = "hash" };
        pairing.Signals.Add(new AgentSignal { Nonce = Guid.NewGuid().ToString("N"), SignedAt = ExamStart, SignatureValid = true });
        db.AgentPairings.Add(pairing);
        db.Verifications.Add(new Verification { ExamSessionId = session.Id, Kind = VerificationKind.Initial, Passed = true });
        await db.SaveChangesAsync();
    }

    private static ConsentRecord Consent(User user, DateTimeOffset grantedAt, DateTimeOffset? withdrawnAt = null) => new()
    {
        User = user, Type = ConsentType.Biometric, TextVersion = ConsentText.InitialBiometricVersion,
        GrantedAt = grantedAt, WithdrawnAt = withdrawnAt,
    };

    private static async Task TemplateAsync(AppDbContext db, Guid userId, DateTimeOffset createdAt)
    {
        var consent = new ConsentRecord
        {
            UserId = userId, Type = ConsentType.Biometric, TextVersion = ConsentText.InitialBiometricVersion, GrantedAt = createdAt,
        };
        db.ReferenceFaceTemplates.Add(new ReferenceFaceTemplate { UserId = userId, EncryptedVector = [1, 2, 3], ConsentRecord = consent, CreatedAt = createdAt });
        await db.SaveChangesAsync();
    }

    private static Task<bool> HasTemplateAsync(AppDbContext db, Guid userId) => db.ReferenceFaceTemplates.AnyAsync(t => t.UserId == userId);
}
