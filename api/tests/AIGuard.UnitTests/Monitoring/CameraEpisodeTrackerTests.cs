using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Monitoring;
using AIGuard.Domain.Enums;

namespace AIGuard.UnitTests.Monitoring;

/// <summary>
/// Kamera olay kuralları ve bölüm mantığı (S-20, K-45, docs/contracts.md 5. bölüm). Ölçümler saniyede bir;
/// harfler: F = tek yüz, bakış yok; G = tek yüz, bakış sapmış; 0 = yüz yok.
/// </summary>
public class CameraEpisodeTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);

    private readonly CameraEpisodeTracker _tracker = new(new CameraMonitorOptions());

    private int _second;

    internal static FrameMeasurement Face(bool gaze = false, double yaw = 0, byte[]? evidence = null) =>
        new(1, gaze ? yaw == 0 ? 40 : yaw : 2, -3, gaze, null, null, evidence ?? (gaze ? [1] : null));

    internal static FrameMeasurement NoFace(byte[]? evidence = null) => new(0, null, null, null, null, null, evidence ?? [0]);

    private static DateTimeOffset At(int second) => T0.AddSeconds(second);

    /// <summary>Ölçümleri sırayla verir; çıkan bölümleri toplar.</summary>
    private List<CameraEpisode> Feed(params FrameMeasurement[] measurements)
    {
        var closed = new List<CameraEpisode>();
        foreach (var m in measurements)
        {
            closed.AddRange(_tracker.Apply(At(_second++), m));
        }

        return closed;
    }

    /// <summary>"GG0GGF" gibi bir diziyi ölçümlere çevirir.</summary>
    private List<CameraEpisode> Feed(string pattern) =>
        Feed(pattern.Select(c => c switch
        {
            'G' => Face(gaze: true),
            'F' => Face(),
            '0' => NoFace(),
            _ => throw new ArgumentException(pattern),
        }).ToArray());

    private static JsonElement Payload(CameraEpisode e) => JsonDocument.Parse(e.Payload).RootElement;

    // ---------- FACE_MISSING ----------

    [Fact]
    public void Two_missing_faces_are_not_an_event()
    {
        Assert.Empty(Feed("F00F"));
        Assert.Empty(_tracker.CloseAll(EpisodeEnd.SessionEnd));
    }

    [Fact]
    public void Three_missing_faces_are_one_face_missing_episode_written_when_the_face_returns()
    {
        Assert.Empty(Feed("F000"));

        var episode = Assert.Single(Feed("F"));

        Assert.Equal(EventType.FACE_MISSING, episode.Type);
        Assert.Equal(At(1), episode.StartedAt);
        Assert.Equal(2, episode.DurationSec); // ilk kayıptan son kayba
        Assert.Null(episode.Confidence);
        Assert.Equal(3, Payload(episode).GetProperty("measurements").GetInt32());
        Assert.Equal(EpisodeEnd.Condition, Payload(episode).GetProperty("closedBy").GetString());
    }

    [Fact]
    public void Long_absence_is_still_a_single_episode()
    {
        var episodes = Feed("0000000000F");

        var episode = Assert.Single(episodes);
        Assert.Equal(9, episode.DurationSec);
        Assert.Equal(10, Payload(episode).GetProperty("measurements").GetInt32());
    }

    // ---------- GAZE_AWAY ----------

    [Fact]
    public void Four_gaze_away_measurements_are_not_an_event()
    {
        Assert.Empty(Feed("GGGGF"));
    }

    [Fact]
    public void Five_gaze_away_measurements_are_one_episode()
    {
        var episode = Assert.Single(Feed("GGGGGF"));

        Assert.Equal(EventType.GAZE_AWAY, episode.Type);
        Assert.Equal(At(0), episode.StartedAt);
        Assert.Equal(4, episode.DurationSec);
        Assert.Equal(5, Payload(episode).GetProperty("measurements").GetInt32());
        Assert.Equal(0, Payload(episode).GetProperty("faceLostMeasurements").GetInt32());
    }

    [Fact]
    public void A_single_lost_face_continues_the_gaze_series_but_is_not_counted()
    {
        // G G 0 G G: seri sürüyor ama yalnızca 4 bakış ölçümü var → olay yok.
        Assert.Empty(Feed("GG0GGF"));
    }

    [Fact]
    public void A_single_lost_face_inside_the_series_does_not_reset_it()
    {
        // G G 0 G G G: kayıp seriyi sıfırlamadı; 5. bakış ölçümünde GAZE_AWAY.
        var episode = Assert.Single(Feed("GG0GGGF"));

        Assert.Equal(EventType.GAZE_AWAY, episode.Type);
        Assert.Equal(At(0), episode.StartedAt);
        Assert.Equal(5, episode.DurationSec); // köprü süreye dahil, bitiş son G
        Assert.Equal(5, Payload(episode).GetProperty("measurements").GetInt32());
        Assert.Equal(1, Payload(episode).GetProperty("faceLostMeasurements").GetInt32());
    }

    [Fact]
    public void Two_consecutive_lost_faces_also_continue_the_series()
    {
        var episode = Assert.Single(Feed("GG00GGGF"));

        Assert.Equal(EventType.GAZE_AWAY, episode.Type);
        Assert.Equal(2, Payload(episode).GetProperty("faceLostMeasurements").GetInt32());
    }

    [Fact]
    public void Alternating_lost_faces_while_looking_away_produce_gaze_not_face_missing()
    {
        // K-28 kamera denemesi: baş çok dönükken yüz 1 → 0 → 1.
        var episodes = Feed("G0G0G0G0GF");

        var episode = Assert.Single(episodes);
        Assert.Equal(EventType.GAZE_AWAY, episode.Type);
        Assert.Equal(4, Payload(episode).GetProperty("faceLostMeasurements").GetInt32());
    }

    [Fact]
    public void Third_consecutive_lost_face_ends_the_gaze_series_and_starts_face_missing()
    {
        // Onaylı bakış serisi üçüncü kayıpta kapanıyor; bitişi son G.
        var gaze = Assert.Single(Feed("GGGGG000"));
        Assert.Equal(EventType.GAZE_AWAY, gaze.Type);
        Assert.Equal(At(4), gaze.StartedAt.AddSeconds(gaze.DurationSec));

        // FACE_MISSING ilk kayıptan başlıyor, yüz gelince yazılıyor.
        var missing = Assert.Single(Feed("F"));
        Assert.Equal(EventType.FACE_MISSING, missing.Type);
        Assert.Equal(At(5), missing.StartedAt);
        Assert.Equal(2, missing.DurationSec);
    }

    [Fact]
    public void Unconfirmed_gaze_series_is_dropped_when_the_face_is_missing_three_times()
    {
        var episodes = Feed("GG000F");

        var episode = Assert.Single(episodes);
        Assert.Equal(EventType.FACE_MISSING, episode.Type);
    }

    [Fact]
    public void Lost_faces_before_the_series_do_not_bridge_into_it()
    {
        // 0 G G G G: kayıp seriden önce geldi, seriye ait değil; 4 bakış → olay yok.
        Assert.Empty(Feed("0GGGGF"));
    }

    [Fact]
    public void Looking_back_after_lost_faces_closes_the_series()
    {
        // G G G G G 0 F: tek kayıptan sonra yüz geri geldi ama bakmıyor; seri son G'de bitiyor.
        var episode = Assert.Single(Feed("GGGGG0F"));

        Assert.Equal(4, episode.DurationSec);
        Assert.Equal(0, Payload(episode).GetProperty("faceLostMeasurements").GetInt32());
    }

    [Fact]
    public void Gaze_payload_has_the_largest_angles_and_no_image()
    {
        var episode = Assert.Single(Feed(
            Face(gaze: true, yaw: 35), Face(gaze: true, yaw: -58.123456), Face(gaze: true, yaw: 40),
            Face(gaze: true, yaw: 33), Face(gaze: true, yaw: 31), Face()));

        Assert.Equal(58.1235, Payload(episode).GetProperty("maxAbsYaw").GetDouble());
        Assert.Equal(3, Payload(episode).GetProperty("maxAbsPitch").GetDouble());
        Assert.DoesNotContain("evidence", episode.Payload, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- MULTIPLE_FACES ----------

    [Fact]
    public void Two_faces_in_one_measurement_is_an_instant_episode()
    {
        var episodes = Feed(Face(), new FrameMeasurement(2, 1, 1, false, null, null, [2]), Face());

        var episode = Assert.Single(episodes);
        Assert.Equal(EventType.MULTIPLE_FACES, episode.Type);
        Assert.Equal(At(1), episode.StartedAt);
        Assert.Equal(0, episode.DurationSec);
        Assert.Equal(2, Payload(episode).GetProperty("maxFaces").GetInt32());
    }

    [Fact]
    public void Consecutive_multiple_faces_merge()
    {
        var episodes = Feed(
            new FrameMeasurement(2, 1, 1, false, null, null, [2]),
            new FrameMeasurement(3, 1, 1, false, null, null, [3]),
            Face());

        var episode = Assert.Single(episodes);
        Assert.Equal(1, episode.DurationSec);
        Assert.Equal(3, Payload(episode).GetProperty("maxFaces").GetInt32());
        Assert.Equal([2], episode.Evidence); // bölümün ilk karesi
    }

    // ---------- PHONE ----------

    private static FrameMeasurement Phone(bool detected, double confidence, byte[]? evidence = null) =>
        new(1, 0, 0, false, new PhoneReading(detected, confidence), null, detected ? evidence ?? [9] : null);

    [Fact]
    public void Phone_detections_three_seconds_apart_merge_into_one_episode()
    {
        // Telefon 3 sn'de bir kontrol ediliyor; aradaki kareler (phone = null) bölümü kesmiyor.
        var episodes = Feed(
            Phone(true, 0.62, [7]), Face(), Face(),
            Phone(true, 0.81, [8]), Face(), Face(),
            Phone(false, 0.10));

        var episode = Assert.Single(episodes);
        Assert.Equal(EventType.PHONE, episode.Type);
        Assert.Equal(At(0), episode.StartedAt);
        Assert.Equal(3, episode.DurationSec); // son tespite kadar
        Assert.Equal(0.81, episode.Confidence);
        Assert.Equal(2, Payload(episode).GetProperty("measurements").GetInt32());
        Assert.Equal([7], episode.Evidence);
    }

    [Fact]
    public void Phone_below_threshold_is_not_an_event()
    {
        Assert.Empty(Feed(Phone(false, 0.37), Face(), Phone(false, 0.2)));
        Assert.Empty(_tracker.CloseAll(EpisodeEnd.SessionEnd));
    }

    // ---------- IDENTITY_MISMATCH ----------

    private static FrameMeasurement Identity(bool matched, double similarity) =>
        new(1, 0, 0, false, null, new IdentityReading(matched, similarity), matched ? null : [5]);

    [Fact]
    public void Identity_mismatch_lasts_until_a_matching_check_and_ignores_frames_without_identity()
    {
        var closed = new List<CameraEpisode>();
        closed.AddRange(_tracker.Apply(At(0), Identity(false, 0.05)));
        closed.AddRange(_tracker.Apply(At(30), Face()));
        closed.AddRange(_tracker.Apply(At(60), Identity(false, 0.12)));
        // Yüz yok → identity null: bölüme dokunmuyor.
        closed.AddRange(_tracker.Apply(At(61), NoFace()));
        Assert.Empty(closed);

        var episode = Assert.Single(_tracker.Apply(At(120), Identity(true, 0.7)));

        Assert.Equal(EventType.IDENTITY_MISMATCH, episode.Type);
        Assert.Equal(At(0), episode.StartedAt);
        Assert.Equal(60, episode.DurationSec);
        Assert.Equal(1 - 0.12, episode.Confidence!.Value, 6); // 1 − en yüksek benzerlik
        Assert.Equal(0.05, Payload(episode).GetProperty("minSimilarity").GetDouble());
        Assert.Equal(0.12, Payload(episode).GetProperty("maxSimilarity").GetDouble());
    }

    [Fact]
    public void Identity_confidence_stays_within_zero_and_one()
    {
        _tracker.Apply(At(0), Identity(false, -0.4));

        var episode = Assert.Single(_tracker.CloseAll(EpisodeEnd.SessionEnd));
        Assert.Equal(1, episode.Confidence);
    }

    // ---------- kapatma ----------

    [Fact]
    public void Close_all_writes_confirmed_episodes_and_drops_unconfirmed_series()
    {
        Feed(Phone(true, 0.9), Face(gaze: true), Face(gaze: true), Face(gaze: true));

        var episodes = _tracker.CloseAll(EpisodeEnd.SessionEnd);

        var phone = Assert.Single(episodes);
        Assert.Equal(EventType.PHONE, phone.Type);
        Assert.Equal(EpisodeEnd.SessionEnd, Payload(phone).GetProperty("closedBy").GetString());
        Assert.False(_tracker.HasOpen);
    }

    [Fact]
    public void Close_all_writes_an_ongoing_face_missing_episode()
    {
        Feed("F0000");

        var episode = Assert.Single(_tracker.CloseAll(EpisodeEnd.Stale));

        Assert.Equal(EventType.FACE_MISSING, episode.Type);
        Assert.Equal(3, episode.DurationSec);
    }

    [Fact]
    public void Tracker_is_stale_only_with_an_open_series_and_after_the_gap()
    {
        Feed("F");
        Assert.False(_tracker.IsStale(At(100)));

        Feed("0");
        Assert.False(_tracker.IsStale(At(1 + 9)));
        Assert.True(_tracker.IsStale(At(1 + 10)));
    }

    [Fact]
    public void Each_episode_keeps_only_its_first_evidence_frame()
    {
        var episode = Assert.Single(Feed(NoFace([1]), NoFace([2]), NoFace([3]), Face()));

        Assert.Equal([1], episode.Evidence);
    }

    [Fact]
    public void Thresholds_come_from_options()
    {
        var tracker = new CameraEpisodeTracker(new CameraMonitorOptions { FaceMissingMeasurements = 2, GazeAwayMeasurements = 2 });

        tracker.Apply(At(0), NoFace());
        tracker.Apply(At(1), NoFace());
        Assert.Equal(EventType.FACE_MISSING, Assert.Single(tracker.Apply(At(2), Face(gaze: true))).Type);

        tracker.Apply(At(3), Face(gaze: true));
        Assert.Equal(EventType.GAZE_AWAY, Assert.Single(tracker.Apply(At(4), Face())).Type);
    }
}
