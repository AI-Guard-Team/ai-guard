using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Monitoring;
using AIGuard.Application.Sessions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Domain.Sessions;
using AIGuard.Infrastructure.Persistence;
using AIGuard.IntegrationTests.Biometrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace AIGuard.IntegrationTests.Monitoring;

/// <summary>
/// Kamera bölümlerinin veritabanına yazılması (K-45): olay + şifreli kanıt karesi, referans vektörün çözülmesi,
/// teslim ve otomatik teslimde açık bölümlerin oturum kapanmadan yazılması.
/// </summary>
public class CameraMonitorIntegrationTests(PostgresFixture fx) : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly FakeTimeProvider _clock = Clock(); // EventTestHelpers.Now
    private readonly MemoryEvidenceStore _evidence = new();
    private readonly CameraMonitorStore _store = new();
    private readonly CameraMonitorOptions _options = new();
    private readonly List<ServiceProvider> _providers = [];

    private CameraMonitorStorage Storage(AppDbContext db) =>
        new(db, Service(db, _clock), _evidence, BiometricTestHelpers.Encryptor, _clock);

    /// <summary>
    /// Temizlik turunun oturum başına açtığı kapsamlar: her kapsam kendi AppDbContext'i ve depolamasıyla, uygulamadaki
    /// kayıt gibi. <paramref name="onContextCreated"/> kapsamın bağlamı oluşunca çağrılıyor (sızıntı testi için).
    /// </summary>
    private IServiceScopeFactory Scopes(Action<AppDbContext>? onContextCreated = null)
    {
        var provider = new ServiceCollection()
            .AddScoped(_ =>
            {
                var db = fx.CreateContext();
                onContextCreated?.Invoke(db);
                return db;
            })
            .AddScoped<ICameraMonitorStorage>(sp => Storage(sp.GetRequiredService<AppDbContext>()))
            .BuildServiceProvider();
        _providers.Add(provider);
        return provider.GetRequiredService<IServiceScopeFactory>();
    }

    private CameraEpisodeCloser Closer(AppDbContext db, IServiceScopeFactory? scopes = null) =>
        new(_store, () => Storage(db), scopes ?? Scopes(), MsOptions.Create(_options), _clock, NullLogger<CameraEpisodeCloser>.Instance);

    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private CameraMonitor Monitor(AppDbContext db, IFrameAnalysisService vision) =>
        new(_store, vision, () => Storage(db), () => true, MsOptions.Create(_options), NullLogger<CameraMonitor>.Instance);

    private async Task<ExamSession> SessionAsync()
    {
        await using var db = fx.CreateContext();
        return await InProgressSessionAsync(db);
    }

    private async Task<List<Event>> EventsAsync(Guid sessionId)
    {
        await using var db = fx.CreateContext();
        return await db.Events.AsNoTracking().Include(e => e.EvidenceFrame).Where(e => e.ExamSessionId == sessionId).ToListAsync();
    }

    private static CameraEpisode Phone(byte[]? evidence = null, double duration = 6) => new(
        EventType.PHONE, Now.AddSeconds(-duration), duration, 0.81, JsonSerializer.Serialize(new { measurements = 3, closedBy = "condition" }), evidence);

    // ---------- yazım ----------

    [SkippableFact]
    public async Task Episode_is_one_event_with_its_encrypted_evidence_frame()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionAsync();

        await using (var db = fx.CreateContext())
        {
            Assert.Equal(EpisodeWriteStatus.Recorded, await Storage(db).RecordAsync(session.Id, Phone([1, 2, 3])));
        }

        var e = Assert.Single(await EventsAsync(session.Id));
        Assert.Equal(EventType.PHONE, e.Type);
        Assert.Equal(EventSource.Camera, e.Source);
        Assert.Equal(Now.AddSeconds(-6), e.StartedAt);
        Assert.Equal(6, e.DurationSec);
        Assert.Equal(0.81, e.Confidence);
        Assert.Contains("\"measurements\": 3", e.Payload);

        var frame = e.EvidenceFrame!;
        Assert.Equal(Now.AddDays(session.Exam.RetentionDays), frame.DeleteAfter);
        Assert.Equal([1, 2, 3], _evidence.Files[frame.StorageKey]);
    }

    [SkippableFact]
    public async Task Episode_without_evidence_has_no_frame()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionAsync();

        await using (var db = fx.CreateContext())
        {
            await Storage(db).RecordAsync(session.Id, Phone());
        }

        Assert.Null(Assert.Single(await EventsAsync(session.Id)).EvidenceFrame);
        Assert.Empty(_evidence.Files);
    }

    [SkippableFact]
    public async Task Episode_longer_than_an_hour_is_written_at_the_limit()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionAsync();

        await using (var db = fx.CreateContext())
        {
            Assert.Equal(EpisodeWriteStatus.Recorded, await Storage(db).RecordAsync(session.Id, Phone(duration: 4000)));
        }

        Assert.Equal(3600, Assert.Single(await EventsAsync(session.Id)).DurationSec);
    }

    [SkippableFact]
    public async Task Closed_session_rejects_the_episode_and_its_frame_is_deleted()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, SessionState.Completed);

        Assert.Equal(EpisodeWriteStatus.SessionClosed, await Storage(db).RecordAsync(session.Id, Phone([1])));
        Assert.Equal(EpisodeWriteStatus.SessionClosed, await Storage(db).RecordAsync(Guid.NewGuid(), Phone([1])));

        Assert.Empty(_evidence.Files);
        Assert.Empty(await EventsAsync(session.Id));
    }

    // ---------- vektör ve oturum durumu ----------

    [SkippableFact]
    public async Task Reference_vector_is_decrypted_from_the_candidates_template()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();

        // Kayıt fotoğrafı oturum doğrulama adımına gelmeden yükleniyor; sonrasında kayıt SessionActive ile
        // reddediliyor (K-27). Yardımcı kaydın başarılı olduğunu doğruluyor, sonra oturumu sınav sırasına alıyor.
        var session = await BiometricTestHelpers.PendingSessionAsync(db, SessionState.InProgress, enrolled: true);
        var other = await BiometricTestHelpers.PendingSessionAsync(db, SessionState.InProgress, enrolled: false);
        Assert.True(await db.ReferenceFaceTemplates.AnyAsync(t => t.UserId == session.CandidateId));

        Assert.Equal(BiometricTestHelpers.Vector(1), await Storage(db).LoadReferenceVectorAsync(session.Id));
        Assert.Null(await Storage(db).LoadReferenceVectorAsync(other.Id)); // kayıt fotoğrafı yok
    }

    [SkippableFact]
    public async Task Ended_sessions_are_found()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var running = await InProgressSessionAsync(db);
        var disconnected = await InProgressSessionAsync(db, SessionState.Disconnected);
        var cancelled = await InProgressSessionAsync(db, SessionState.Cancelled);
        var missing = Guid.NewGuid();

        var ended = await Storage(db).FindEndedAsync([running.Id, disconnected.Id, cancelled.Id, missing]);

        Assert.Equal(new[] { cancelled.Id, missing }.Order(), ended.Order());
    }

    // ---------- oturum bitişi (SessionService, SessionTimekeeper) ----------

    private async Task FeedMultipleFacesAsync(Guid sessionId)
    {
        await using var db = fx.CreateContext();
        var monitor = Monitor(db, new TwoFacesVision());
        await monitor.AnalyzeFrameAsync(sessionId, [0xFF, 0xD8, 0xFF], Now.AddSeconds(-2));
        await monitor.AnalyzeFrameAsync(sessionId, [0xFF, 0xD8, 0xFF], Now.AddSeconds(-1));
    }

    [SkippableFact]
    public async Task Submitting_writes_the_open_episode_before_the_session_closes()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionAsync();
        await FeedMultipleFacesAsync(session.Id);
        Assert.Empty(await EventsAsync(session.Id));

        await using (var db = fx.CreateContext())
        {
            var events = Service(db, _clock);
            var result = await new SessionService(db, events, _clock, monitoring: Closer(db)).SubmitAsync(session.Id);
            Assert.Equal(SessionOperationStatus.Success, result.Status);
        }

        var e = Assert.Single(await EventsAsync(session.Id));
        Assert.Equal(EventType.MULTIPLE_FACES, e.Type);
        Assert.Equal(Now.AddSeconds(-2), e.StartedAt);
        Assert.Equal(1, e.DurationSec);
        Assert.Contains("\"closedBy\":\"sessionEnd\"", e.Payload!.Replace(" ", ""));
        Assert.NotNull(e.EvidenceFrame);

        // Durum kapandı; vektör bellekte değil.
        Assert.True(_store.TryGet(session.Id, out var state));
        Assert.True(state.Closed);
        Assert.Null(state.Reference);

        await using var check = fx.CreateContext();
        Assert.Equal(SessionState.Completed, (await check.ExamSessions.SingleAsync(s => s.Id == session.Id)).State);
    }

    [SkippableFact]
    public async Task Automatic_submission_writes_the_open_episode()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionAsync();
        await FeedMultipleFacesAsync(session.Id);

        _clock.Advance(TimeSpan.FromMinutes(31));
        await using (var db = fx.CreateContext())
        {
            await new SessionTimekeeper(db, Service(db, _clock), _clock, monitoring: Closer(db)).RunOnceAsync();
        }

        var e = Assert.Single(await EventsAsync(session.Id));
        Assert.Equal(EventType.MULTIPLE_FACES, e.Type);

        await using var check = fx.CreateContext();
        Assert.Equal(SessionState.Completed, (await check.ExamSessions.SingleAsync(s => s.Id == session.Id)).State);
    }

    [SkippableFact]
    public async Task Submitting_a_session_without_camera_state_is_unchanged()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionAsync();

        await using (var db = fx.CreateContext())
        {
            var result = await new SessionService(db, Service(db, _clock), _clock, monitoring: Closer(db)).SubmitAsync(session.Id);
            Assert.Equal(SessionOperationStatus.Success, result.Status);
        }

        Assert.Empty(await EventsAsync(session.Id));
    }

    [SkippableFact]
    public async Task Cancelled_session_state_is_dropped_by_the_sweep_without_events()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionAsync();
        await FeedMultipleFacesAsync(session.Id);

        await using (var db = fx.CreateContext())
        {
            var row = await db.ExamSessions.SingleAsync(s => s.Id == session.Id);
            row.TransitionTo(SessionState.Cancelled);
            await db.SaveChangesAsync();
        }

        await using (var db = fx.CreateContext())
        {
            await Closer(db).SweepAsync();
        }

        Assert.False(_store.TryGet(session.Id, out _));
        Assert.Empty(await EventsAsync(session.Id));
    }

    [SkippableFact]
    public async Task Sweep_failure_in_one_session_does_not_leak_into_the_next_sessions_write()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var first = await SessionAsync();
        var second = await SessionAsync();
        await FeedMultipleFacesAsync(first.Id);
        await FeedMultipleFacesAsync(second.Id);

        // Kareler kesildi: iki oturumun açık bölümü temizlik turunda yazılacak.
        _clock.Advance(TimeSpan.FromSeconds(_options.StaleAfterSeconds + 5));

        // Turda açılan ilk kapsamın bağlamı bozuk: var olmayan olaya bağlı kanıt karesi (FK ihlali). O oturumun
        // SaveChanges'ı başarısız oluyor ve varlık change tracker'da kalıyor. Bağlam ortak olsaydı ikinci oturumun
        // yazımı da aynı varlığı kaydetmeye çalışıp bozulurdu.
        var contexts = 0;
        var scopes = Scopes(db =>
        {
            if (Interlocked.Increment(ref contexts) == 1)
            {
                db.EvidenceFrames.Add(new EvidenceFrame
                {
                    EventId = Guid.NewGuid(),
                    StorageKey = $"{Guid.NewGuid():N}.bin",
                    DeleteAfter = Now.AddDays(1),
                });
            }
        });

        await using (var db = fx.CreateContext())
        {
            await Closer(db, scopes).SweepAsync(); // istisna dışarı çıkmıyor
        }

        // Hangi oturum önce işlendiyse onunki yazılamadı; diğeri kendi bağlamıyla yazıldı.
        var firstEvents = await EventsAsync(first.Id);
        var secondEvents = await EventsAsync(second.Id);
        Assert.Equal(2, contexts);
        Assert.Equal(1, firstEvents.Count + secondEvents.Count);
        Assert.Equal(EventType.MULTIPLE_FACES, Assert.Single(firstEvents.Concat(secondEvents)).Type);

        // Yazılamayan bölümün kanıt dosyası geri silindi (K-26); depoda yalnızca yazılan olayınki var.
        Assert.Single(_evidence.Files);
    }

    /// <summary>Her karede iki yüz; kimlik ve telefon sonuçları sözleşmeye uygun.</summary>
    private sealed class TwoFacesVision : IFrameAnalysisService
    {
        public Task<FrameAnalysisResult> AnalyzeAsync(
            Guid sessionId, byte[] frame, DateTimeOffset ts, bool phone, float[]? referenceVector, CancellationToken ct = default) =>
            Task.FromResult(FrameAnalysisResult.Ok(new FrameMeasurement(
                2, 1, 1, false,
                phone ? new PhoneReading(false, 0.1) : null,
                referenceVector is not null ? new IdentityReading(true, 0.7) : null,
                frame)));
    }
}
