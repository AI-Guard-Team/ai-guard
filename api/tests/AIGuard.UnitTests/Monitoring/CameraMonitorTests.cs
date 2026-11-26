using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Monitoring;
using AIGuard.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace AIGuard.UnitTests.Monitoring;

/// <summary>
/// Kamera analizinin zamanlaması, MONITORING_GAP, kanıt karesi, oturum bitişi ve durum temizliği (K-45).
/// Görüntü servisi ve veritabanı sahte; saat her karenin alınma anı ve temizlik için FakeTimeProvider.
/// </summary>
public class CameraMonitorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Session = Guid.NewGuid();

    private readonly CameraMonitorOptions _options = new();
    private readonly CameraMonitorStore _store = new();
    private readonly FakeVision _vision = new();
    private readonly FakeStorage _storage = new();
    private readonly FakeTimeProvider _clock = new(T0);

    private CameraMonitor Monitor() =>
        new(_store, _vision, _storage, MsOptions.Create(_options), NullLogger<CameraMonitor>.Instance);

    private CameraEpisodeCloser Closer() =>
        new(_store, () => _storage, MsOptions.Create(_options), _clock, NullLogger<CameraEpisodeCloser>.Instance);

    private Task Frame(double second, Guid? session = null) =>
        Monitor().AnalyzeFrameAsync(session ?? Session, [0xFF, 0xD8, 0xFF], T0.AddSeconds(second));

    private async Task Frames(int from, int to)
    {
        for (var s = from; s <= to; s++)
        {
            await Frame(s);
        }
    }

    private List<CameraEpisode> Recorded(EventType? type = null) =>
        _storage.Recorded.Select(r => r.Episode).Where(e => type is null || e.Type == type).ToList();

    private static FrameAnalysisResult Measure(
        bool phone, float[]? reference, int faces = 1, bool gaze = false, bool phoneDetected = false,
        bool? matched = true, byte[]? evidence = null) =>
        FrameAnalysisResult.Ok(new FrameMeasurement(
            faces,
            faces > 0 ? (gaze ? 45 : 1) : null,
            faces > 0 ? 0 : null,
            faces > 0 ? gaze : null,
            phone ? new PhoneReading(phoneDetected, phoneDetected ? 0.9 : 0.1) : null,
            reference is not null && faces > 0 && matched is { } m ? new IdentityReading(m, m ? 0.7 : 0.05) : null,
            evidence));

    // ---------- zamanlama ----------

    [Fact]
    public async Task Face_and_pose_run_on_every_frame_phone_every_three_seconds()
    {
        await Frames(0, 7);

        Assert.Equal(8, _vision.Calls.Count);
        Assert.Equal([0, 3, 6], _vision.Calls.Where(c => c.Phone).Select(c => (c.Ts - T0).TotalSeconds));
    }

    [Fact]
    public async Task Identity_runs_on_the_first_frame_and_then_every_sixty_seconds()
    {
        await Frame(0);
        await Frame(30);
        await Frame(59.9);
        await Frame(60);
        await Frame(61);

        Assert.Equal([0, 60], _vision.Calls.Where(c => c.Identity).Select(c => (c.Ts - T0).TotalSeconds));
    }

    [Fact]
    public async Task Reference_vector_is_decrypted_once_per_session()
    {
        await Frame(0);
        await Frame(60);
        await Frame(120);

        Assert.Equal(3, _vision.Calls.Count(c => c.Identity));
        Assert.Equal(1, _storage.Loads);
    }

    [Fact]
    public async Task Missing_reference_vector_disables_identity_for_the_session()
    {
        _storage.Vector = null;

        await Frame(0);
        await Frame(60);
        await Frame(120);

        Assert.DoesNotContain(_vision.Calls, c => c.Identity);
        Assert.Equal(1, _storage.Loads);
    }

    [Fact]
    public async Task Phone_time_advances_only_when_a_phone_result_arrives()
    {
        // İlk telefon denemesi servis hatası, ikincisi çözülemeyen kare: ikisi de ölçüm değil.
        _vision.Script(
            (_, _) => FrameAnalysisResult.Unavailable(503, "MODELS_NOT_READY"),
            (_, _) => FrameAnalysisResult.InvalidImage());

        await Frames(0, 5);

        // 0 (hata) → 1 (geçersiz kare) → 2 (ölçüm) → 5.
        Assert.Equal([0, 1, 2, 5], _vision.Calls.Where(c => c.Phone).Select(c => (c.Ts - T0).TotalSeconds));
    }

    [Fact]
    public async Task Identity_time_advances_only_on_a_matched_true_or_false_result()
    {
        // Yüz yok (identity = null), sonra servis hatası, sonra geçersiz kare: hepsinde sonraki karede yeniden deneniyor.
        _vision.Script(
            (phone, reference) => Measure(phone, reference, faces: 0),
            (_, _) => FrameAnalysisResult.Unavailable(null, "TIMEOUT"),
            (_, _) => FrameAnalysisResult.InvalidImage(),
            (phone, reference) => Measure(phone, reference, matched: false));

        await Frames(0, 4);
        await Frame(62);
        await Frame(63);

        Assert.Equal([0, 1, 2, 3, 63], _vision.Calls.Where(c => c.Identity).Select(c => (c.Ts - T0).TotalSeconds));
    }

    [Fact]
    public async Task Concurrent_frames_do_not_request_the_same_check_twice()
    {
        var release = new TaskCompletionSource();
        _vision.Script(async (phone, reference) =>
        {
            await release.Task;
            return Measure(phone, reference);
        });

        // İlk kare telefon ve kimlik kontrolünü üstlendi, yanıt bekliyor.
        var first = Frame(0);
        await _vision.WaitForCallsAsync(1);
        await Frame(0.3);

        release.SetResult();
        await first;

        Assert.True(_vision.Calls[0].Phone && _vision.Calls[0].Identity);
        Assert.False(_vision.Calls[1].Phone || _vision.Calls[1].Identity);

        // Yolda işareti kalkınca zamanlama normal: telefon 3 sn, kimlik 60 sn sonra.
        await Frame(1);
        Assert.False(_vision.Calls[2].Phone || _vision.Calls[2].Identity);
        await Frame(3);
        Assert.True(_vision.Calls[3].Phone);
    }

    // ---------- olaylar ve kanıt karesi ----------

    [Fact]
    public async Task Episode_is_written_once_with_its_first_evidence_frame()
    {
        // Telefon 0., 3. ve 6. sn'de görüldü, her seferinde farklı kare; 9. sn'de yok.
        _vision.Respond = (phone, reference) => Measure(phone, reference, phoneDetected: phone,
            evidence: phone ? [(byte)_vision.Calls.Count] : null);
        await Frames(0, 8);
        Assert.Empty(Recorded());

        _vision.Respond = (phone, reference) => Measure(phone, reference);
        await Frame(9);

        var episode = Assert.Single(Recorded());
        Assert.Equal(EventType.PHONE, episode.Type);
        Assert.Equal(T0, episode.StartedAt);
        Assert.Equal(6, episode.DurationSec);
        Assert.Equal([1], episode.Evidence); // ilk tespitin karesi
    }

    [Fact]
    public async Task Suspicious_frames_without_an_event_keep_no_evidence()
    {
        // İki kayıp FACE_MISSING değil; kareleri hiçbir yere yazılmıyor.
        _vision.Script(
            (phone, reference) => Measure(phone, reference, faces: 0, evidence: [1]),
            (phone, reference) => Measure(phone, reference, faces: 0, evidence: [2]));

        await Frames(0, 3);
        await Closer().CloseSessionAsync(Session);

        Assert.Empty(_storage.Recorded);
    }

    // ---------- MONITORING_GAP ----------

    [Fact]
    public async Task Vision_outage_is_a_single_monitoring_gap_written_on_the_first_success()
    {
        _vision.Respond = (_, _) => FrameAnalysisResult.Unavailable(503, "MODELS_NOT_READY");
        await Frames(0, 4);
        Assert.Empty(Recorded());

        _vision.Respond = (phone, reference) => Measure(phone, reference);
        await Frames(5, 7);

        var gap = Assert.Single(Recorded());
        Assert.Equal(EventType.MONITORING_GAP, gap.Type);
        Assert.Equal(T0, gap.StartedAt);
        Assert.Equal(5, gap.DurationSec);
        Assert.Null(gap.Evidence);
        var payload = JsonDocument.Parse(gap.Payload).RootElement;
        Assert.Equal(5, payload.GetProperty("failedFrames").GetInt32());
        Assert.Equal(503, payload.GetProperty("status").GetInt32());
        Assert.Equal("MODELS_NOT_READY", payload.GetProperty("code").GetString());
        Assert.Equal(EpisodeEnd.Recovered, payload.GetProperty("closedBy").GetString());
    }

    [Fact]
    public async Task Each_outage_is_its_own_gap()
    {
        _vision.Script(
            (_, _) => FrameAnalysisResult.Unavailable(null, "UNREACHABLE"),
            (phone, reference) => Measure(phone, reference),
            (_, _) => FrameAnalysisResult.Unavailable(401, "UNAUTHORIZED"),
            (_, _) => FrameAnalysisResult.Unavailable(401, "UNAUTHORIZED"));

        await Frames(0, 4);

        Assert.Equal(2, Recorded(EventType.MONITORING_GAP).Count);
    }

    [Fact]
    public async Task Invalid_image_is_not_a_monitoring_gap_and_does_not_break_a_series()
    {
        // G G G (geçersiz kare) G G: kare atlandı, seri sürdü → GAZE_AWAY; boşluk yok.
        _vision.Script(
            (p, r) => Measure(p, r, gaze: true), (p, r) => Measure(p, r, gaze: true), (p, r) => Measure(p, r, gaze: true),
            (_, _) => FrameAnalysisResult.InvalidImage(),
            (p, r) => Measure(p, r, gaze: true), (p, r) => Measure(p, r, gaze: true));

        await Frames(0, 6);

        Assert.Empty(Recorded(EventType.MONITORING_GAP));
        var gaze = Assert.Single(Recorded(EventType.GAZE_AWAY));
        Assert.Equal(5, gaze.DurationSec);
    }

    [Fact]
    public async Task Gap_ends_at_the_last_failure_when_frames_stopped_in_between()
    {
        _vision.Script(
            (_, _) => FrameAnalysisResult.Unavailable(null, "TIMEOUT"),
            (_, _) => FrameAnalysisResult.Unavailable(null, "TIMEOUT"));
        await Frames(0, 1);

        // 40 sn kare yok (bağlantı kesildi), sonra başarılı kare: kesinti boşluğa eklenmiyor.
        await Frame(41);

        var gap = Assert.Single(Recorded(EventType.MONITORING_GAP));
        Assert.Equal(1, gap.DurationSec);
    }

    // ---------- oturum bitişi ----------

    [Fact]
    public async Task Ending_the_session_writes_open_episodes_and_the_gap_then_wipes_the_vector()
    {
        // Kareler 3 sn dolmadan: telefon yeniden kontrol edilmiyor, telefon bölümü açık kalıyor.
        _vision.Script(
            (p, r) => Measure(p, r, phoneDetected: true, evidence: [4]),
            (p, r) => Measure(p, r, faces: 0), (p, r) => Measure(p, r, faces: 0), (p, r) => Measure(p, r, faces: 0),
            (_, _) => FrameAnalysisResult.Unavailable(503, "MODELS_NOT_READY"));
        foreach (var second in new[] { 0, 0.5, 1, 1.5, 2 })
        {
            await Frame(second);
        }
        Assert.True(_store.TryGet(Session, out var state));
        Assert.NotNull(state.Reference);

        await Closer().CloseSessionAsync(Session);

        Assert.Equal(
            [EventType.FACE_MISSING, EventType.PHONE, EventType.MONITORING_GAP],
            Recorded().Select(e => e.Type).Order());
        Assert.All(Recorded(), e => Assert.Contains($"\"closedBy\":\"{EpisodeEnd.SessionEnd}\"", e.Payload));
        Assert.Equal([4], Recorded(EventType.PHONE).Single().Evidence);
        Assert.True(state.Closed);
        Assert.Null(state.Reference);

        // Kapanıştan sonra gelen kare işlenmiyor, ikinci kapanış bir şey yazmıyor.
        var calls = _vision.Calls.Count;
        await Frame(5);
        await Closer().CloseSessionAsync(Session);
        Assert.Equal(calls, _vision.Calls.Count);
        Assert.Equal(3, Recorded().Count);
    }

    [Fact]
    public async Task Result_arriving_after_the_session_closed_is_dropped()
    {
        var release = new TaskCompletionSource();
        _vision.Script(async (phone, reference) =>
        {
            await release.Task;
            return Measure(phone, reference, faces: 2, evidence: [1]);
        });

        var pending = Frame(0);
        await _vision.WaitForCallsAsync(1);
        await Closer().CloseSessionAsync(Session);
        release.SetResult();
        await pending;
        await Closer().CloseSessionAsync(Session);

        Assert.Empty(_storage.Recorded);
    }

    [Fact]
    public async Task Closing_a_session_without_camera_state_does_nothing()
    {
        var resolved = false;
        var closer = new CameraEpisodeCloser(_store, () => { resolved = true; return _storage; },
            MsOptions.Create(_options), _clock, NullLogger<CameraEpisodeCloser>.Instance);

        await closer.CloseSessionAsync(Guid.NewGuid());

        // Depolama (şifreleyici) hiç çözümlenmedi: anahtarı olmayan ortamda teslim yine çalışıyor.
        Assert.False(resolved);
    }

    [Fact]
    public async Task Closed_session_reported_by_the_event_service_removes_the_state()
    {
        _storage.Status = EpisodeWriteStatus.SessionClosed;
        _vision.Script((p, r) => Measure(p, r, faces: 2));

        await Frames(0, 1);

        Assert.Single(_storage.Recorded);
        Assert.False(_store.TryGet(Session, out _));
    }

    // ---------- temizlik turu ----------

    [Fact]
    public async Task Sweep_writes_open_episodes_when_frames_stop()
    {
        _vision.Respond = (p, r) => Measure(p, r, faces: 0);
        await Frames(0, 4);

        _clock.SetUtcNow(T0.AddSeconds(4 + 9));
        await Closer().SweepAsync();
        Assert.Empty(_storage.Recorded);

        _clock.SetUtcNow(T0.AddSeconds(4 + 10));
        await Closer().SweepAsync();

        var episode = Assert.Single(Recorded());
        Assert.Equal(EventType.FACE_MISSING, episode.Type);
        Assert.Equal(4, episode.DurationSec);
        Assert.Contains($"\"closedBy\":\"{EpisodeEnd.Stale}\"", episode.Payload);
        Assert.True(_store.TryGet(Session, out _)); // durum boşta silme süresine kadar kalıyor
    }

    [Fact]
    public async Task Sweep_closes_a_gap_when_frames_stop()
    {
        _vision.Respond = (_, _) => FrameAnalysisResult.Unavailable(null, "UNREACHABLE");
        await Frames(0, 2);

        _clock.SetUtcNow(T0.AddSeconds(12));
        await Closer().SweepAsync();

        var gap = Assert.Single(Recorded(EventType.MONITORING_GAP));
        Assert.Equal(2, gap.DurationSec);
    }

    [Fact]
    public async Task Sweep_removes_ended_and_cancelled_sessions_without_writing()
    {
        _vision.Respond = (p, r) => Measure(p, r, phoneDetected: true, evidence: [1]);
        await Frame(0);
        Assert.True(_store.TryGet(Session, out var state));
        Assert.NotNull(state.Reference);

        // Biyometrik rıza geri alındı, oturum iptal: olay yazılmıyor, vektör siliniyor.
        _storage.Ended.Add(Session);
        await Closer().SweepAsync();

        Assert.False(_store.TryGet(Session, out _));
        Assert.True(state.Closed);
        Assert.Null(state.Reference);
        Assert.Empty(_storage.Recorded);
    }

    [Fact]
    public async Task Sweep_removes_idle_sessions_and_keeps_active_ones()
    {
        var other = Guid.NewGuid();
        await Frame(0);
        await Frame(100, other);

        _clock.SetUtcNow(T0.AddSeconds(_options.IdleRemoveAfterSeconds));
        await Closer().SweepAsync();

        Assert.False(_store.TryGet(Session, out _));
        Assert.True(_store.TryGet(other, out _));
        Assert.Equal(1, _store.Count);
    }

    [Fact]
    public async Task Sweep_with_no_sessions_does_not_touch_storage()
    {
        var resolved = false;
        var closer = new CameraEpisodeCloser(_store, () => { resolved = true; return _storage; },
            MsOptions.Create(_options), _clock, NullLogger<CameraEpisodeCloser>.Instance);

        await closer.SweepAsync();

        Assert.False(resolved);
    }

    [Fact]
    public async Task Audio_is_not_analyzed_yet()
    {
        await Monitor().AnalyzeAudioAsync(Session, [1, 2, 3], AudioFormat.WebM, T0);

        Assert.Empty(_vision.Calls);
        Assert.Equal(0, _store.Count);
    }

    // ---------- sahteler ----------

    private sealed class FakeVision : IFrameAnalysisService
    {
        private readonly Queue<Func<bool, float[]?, Task<FrameAnalysisResult>>> _script = new();
        private readonly SemaphoreSlim _called = new(0);

        public List<(bool Phone, bool Identity, DateTimeOffset Ts)> Calls { get; } = [];

        /// <summary>Senaryo bitince kullanılan yanıt: tek yüz, bakış yok, telefon yok, kimlik eşleşti.</summary>
        public Func<bool, float[]?, FrameAnalysisResult> Respond { get; set; } = (phone, reference) => Measure(phone, reference);

        public void Script(params Func<bool, float[]?, FrameAnalysisResult>[] steps)
        {
            foreach (var step in steps)
            {
                _script.Enqueue((p, r) => Task.FromResult(step(p, r)));
            }
        }

        public void Script(Func<bool, float[]?, Task<FrameAnalysisResult>> step) => _script.Enqueue(step);

        public async Task WaitForCallsAsync(int count)
        {
            while (true)
            {
                lock (Calls)
                {
                    if (Calls.Count >= count)
                    {
                        return;
                    }
                }

                await _called.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        public Task<FrameAnalysisResult> AnalyzeAsync(
            Guid sessionId, byte[] frame, DateTimeOffset ts, bool phone, float[]? referenceVector, CancellationToken ct = default)
        {
            Func<bool, float[]?, Task<FrameAnalysisResult>>? step;
            lock (Calls)
            {
                Calls.Add((phone, referenceVector is not null, ts));
                _script.TryDequeue(out step);
            }

            _called.Release();
            return step is not null ? step(phone, referenceVector) : Task.FromResult(Respond(phone, referenceVector));
        }
    }

    private sealed class FakeStorage : ICameraMonitorStorage
    {
        public float[]? Vector { get; set; } = UnitVector();

        public int Loads { get; private set; }

        public EpisodeWriteStatus Status { get; set; } = EpisodeWriteStatus.Recorded;

        public List<(Guid SessionId, CameraEpisode Episode)> Recorded { get; } = [];

        public HashSet<Guid> Ended { get; } = [];

        public Task<float[]?> LoadReferenceVectorAsync(Guid sessionId, CancellationToken ct = default)
        {
            Loads++;
            // Her çözüm yeni bir dizi; durum silinince temizlenen kopya bu.
            return Task.FromResult(Vector?.ToArray());
        }

        public Task<EpisodeWriteStatus> RecordAsync(Guid sessionId, CameraEpisode episode, CancellationToken ct = default)
        {
            Recorded.Add((sessionId, episode));
            return Task.FromResult(Status);
        }

        public Task<IReadOnlyCollection<Guid>> FindEndedAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(sessionIds.Where(Ended.Contains).ToList());

        private static float[] UnitVector()
        {
            var v = new float[512];
            v[0] = 1;
            return v;
        }
    }
}
