using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Domain.Enums;

namespace AIGuard.Application.Monitoring;

/// <summary>Bölümün neden kapandığı; olayın payload'ında <c>closedBy</c>.</summary>
public static class EpisodeEnd
{
    /// <summary>Koşul bir sonraki ölçümde sağlanmadı.</summary>
    public const string Condition = "condition";

    /// <summary><see cref="CameraMonitorOptions.StaleAfterSeconds"/> boyunca başarılı ölçüm gelmedi.</summary>
    public const string Stale = "stale";

    /// <summary>Oturum teslim edildi ya da süresi doldu.</summary>
    public const string SessionEnd = "sessionEnd";

    /// <summary>MONITORING_GAP: görüntü servisi yeniden başarılı yanıt verdi.</summary>
    public const string Recovered = "recovered";
}

/// <summary>Yazılmaya hazır bir bölüm: tek olay, süresiyle (K-45).</summary>
/// <param name="StartedAt">Koşulun sağlandığı ilk ölçüm.</param>
/// <param name="DurationSec">İlk ölçümden koşulun sağlandığı son ölçüme (MONITORING_GAP'te ilk başarılı yanıta).</param>
/// <param name="Payload">Görüntü ve vektör içermeyen küçük JSON (K-16, 4 KB'ın çok altında).</param>
/// <param name="Evidence">Bölümün ilk kanıt karesi; olay yazılırsa şifreli saklanıyor (K-26).</param>
public sealed record CameraEpisode(
    EventType Type, DateTimeOffset StartedAt, double DurationSec, double? Confidence, string Payload, byte[]? Evidence);

/// <summary>
/// Bir oturumun kamera ölçümlerinden olay bölümleri üretir (S-20, docs/contracts.md 5. bölüm, K-45).
/// Durumu tek oturuma ait; iş parçacığı güvenli değil, çağıran kilitler. G/Ç yok.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>FACE_MISSING: art arda en az 3 ölçümde faceCount = 0.</item>
/// <item>GAZE_AWAY: bir seride en az 5 gazeAway = true. Seriye giren 1–2 ardışık "yüz yok" ölçümü seriyi
/// sürdürür ama sayılmaz (D4 kamera denemesi, K-28: baş çok dönünce MediaPipe yüzü ara ara kaybediyor);
/// 3. ardışık "yüz yok"ta seri kapanır, FACE_MISSING başlar.</item>
/// <item>MULTIPLE_FACES: faceCount ≥ 2. PHONE: phone.detected. IDENTITY_MISMATCH: identity.matched = false.
/// Görevi çalışmayan kare (alan <c>null</c>) bölüme dokunmuyor.</item>
/// </list>
/// Bölüm, koşulun sağlandığı ilk ölçümde başlar, son ölçümünde biter; eşiğe ulaşmamış seri olay olmadan atılır.
/// </remarks>
public sealed class CameraEpisodeTracker(CameraMonitorOptions options)
{
    private Run? _zeros;
    private Run? _gaze;
    private Run? _multiple;
    private Run? _phone;
    private Run? _identity;

    /// <summary>Son başarılı ölçümün anı.</summary>
    public DateTimeOffset? LastMeasurementAt { get; private set; }

    /// <summary>Henüz kapanmamış (eşiğe ulaşmış ya da ulaşmamış) bir seri var mı.</summary>
    public bool HasOpen => _zeros is not null || _gaze is not null || _multiple is not null || _phone is not null || _identity is not null;

    public bool IsStale(DateTimeOffset now) =>
        HasOpen && LastMeasurementAt is { } last && now - last >= options.StaleAfter;

    public IReadOnlyList<CameraEpisode> Apply(DateTimeOffset at, FrameMeasurement m)
    {
        var closed = new List<CameraEpisode>();
        LastMeasurementAt = at;

        ApplyFaceAndGaze(at, m, closed);

        // Birden fazla yüz yalnızca o ölçümün yüz sayısına bakıyor.
        if (m.FaceCount >= 2)
        {
            var run = Continue(ref _multiple, at, m);
            run.Max = Math.Max(run.Max, m.FaceCount);
        }
        else
        {
            Emit(closed, CloseMultiple(EpisodeEnd.Condition));
        }

        // Telefon kontrolü yapılmayan kare bölümü ne uzatıyor ne kesiyor.
        if (m.Phone is { } phone)
        {
            if (phone.Detected)
            {
                var run = Continue(ref _phone, at, m);
                run.Max = Math.Max(run.Max, phone.Confidence);
            }
            else
            {
                Emit(closed, ClosePhone(EpisodeEnd.Condition));
            }
        }

        // Kimlik kontrolü yapılmayan kare ve identity = null (yüz yok) bölüme dokunmuyor.
        if (m.Identity is { } identity)
        {
            if (!identity.Matched)
            {
                var first = _identity is null;
                var run = Continue(ref _identity, at, m);
                run.Max = first ? identity.Similarity : Math.Max(run.Max, identity.Similarity);
                run.Min = first ? identity.Similarity : Math.Min(run.Min, identity.Similarity);
            }
            else
            {
                Emit(closed, CloseIdentity(EpisodeEnd.Condition));
            }
        }

        return closed;
    }

    /// <summary>Açık bölümleri son ölçümlerine göre kapatır; eşiğe ulaşmamış seriler atılır. Sayaçlar sıfırlanır.</summary>
    public IReadOnlyList<CameraEpisode> CloseAll(string closedBy)
    {
        var closed = new List<CameraEpisode>();
        Emit(closed, CloseZeros(closedBy));
        Emit(closed, CloseGaze(closedBy));
        Emit(closed, CloseMultiple(closedBy));
        Emit(closed, ClosePhone(closedBy));
        Emit(closed, CloseIdentity(closedBy));
        return closed;
    }

    private void ApplyFaceAndGaze(DateTimeOffset at, FrameMeasurement m, List<CameraEpisode> closed)
    {
        if (m.FaceCount == 0)
        {
            var zeros = Continue(ref _zeros, at, m);

            // Üçüncü ardışık kayıp: bakış serisi biter, FACE_MISSING başlar (başlangıcı ilk kayıp).
            if (zeros.Count >= options.FaceMissingMeasurements)
            {
                Emit(closed, CloseGaze(EpisodeEnd.Condition));
            }

            return;
        }

        // Yüz geri geldi. Eşiğe ulaşmış kayıp serisi FACE_MISSING olarak kapanır; ulaşmamışsa ve bakış
        // serisi sürüyorsa kayıplar o seriye köprü oluyor.
        var bridged = 0;
        if (_zeros is not null)
        {
            if (_zeros.Count >= options.FaceMissingMeasurements)
            {
                Emit(closed, CloseZeros(EpisodeEnd.Condition));
            }
            else
            {
                bridged = _zeros.Count;
                _zeros = null;
            }
        }

        if (m.GazeAway == true)
        {
            // Yeni seride köprü yok: kayıplar seriden önce geldiyse seriye ait değil.
            if (_gaze is not null)
            {
                _gaze.Bridged += bridged;
            }

            var run = Continue(ref _gaze, at, m);
            run.Max = Math.Max(run.Max, Math.Abs(m.Yaw ?? 0));
            run.Second = Math.Max(run.Second, Math.Abs(m.Pitch ?? 0));
        }
        else
        {
            Emit(closed, CloseGaze(EpisodeEnd.Condition));
        }
    }

    private CameraEpisode? CloseZeros(string closedBy)
    {
        var run = _zeros;
        _zeros = null;
        if (run is null || run.Count < options.FaceMissingMeasurements)
        {
            return null;
        }

        return run.ToEpisode(EventType.FACE_MISSING, null, new { measurements = run.Count, closedBy });
    }

    private CameraEpisode? CloseGaze(string closedBy)
    {
        var run = _gaze;
        _gaze = null;
        if (run is null || run.Count < options.GazeAwayMeasurements)
        {
            return null;
        }

        return run.ToEpisode(EventType.GAZE_AWAY, null, new
        {
            measurements = run.Count,
            faceLostMeasurements = run.Bridged,
            maxAbsYaw = Round(run.Max),
            maxAbsPitch = Round(run.Second),
            closedBy,
        });
    }

    private CameraEpisode? CloseMultiple(string closedBy)
    {
        var run = _multiple;
        _multiple = null;
        return run?.ToEpisode(EventType.MULTIPLE_FACES, null, new { measurements = run.Count, maxFaces = (int)run.Max, closedBy });
    }

    private CameraEpisode? ClosePhone(string closedBy)
    {
        var run = _phone;
        _phone = null;
        return run?.ToEpisode(EventType.PHONE, Clamp01(run.Max), new { measurements = run.Count, maxConfidence = Round(run.Max), closedBy });
    }

    private CameraEpisode? CloseIdentity(string closedBy)
    {
        var run = _identity;
        _identity = null;

        // Güven = 1 − bölümdeki en yüksek benzerlik: en zayıf kanıta göre, tutucu.
        return run?.ToEpisode(EventType.IDENTITY_MISMATCH, Clamp01(1 - run.Max), new
        {
            measurements = run.Count,
            minSimilarity = Round(run.Min),
            maxSimilarity = Round(run.Max),
            closedBy,
        });
    }

    /// <summary>Seriyi başlatır ya da bir ölçüm uzatır.</summary>
    private static Run Continue(ref Run? run, DateTimeOffset at, FrameMeasurement m) =>
        (run ??= new Run(at)).Extend(at, m.EvidenceFrame);

    private static void Emit(List<CameraEpisode> closed, CameraEpisode? episode)
    {
        if (episode is not null)
        {
            closed.Add(episode);
        }
    }

    private static double Round(double value) => Math.Round(value, 4);

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);

    /// <summary>Bir koşulun ardışık ölçümleri.</summary>
    private sealed class Run(DateTimeOffset start)
    {
        public DateTimeOffset Start { get; } = start;

        public DateTimeOffset Last { get; private set; } = start;

        public int Count { get; private set; }

        /// <summary>
        /// Bölüm başına en fazla bir kare: koşulun sağlandığı ilk karenin kanıtı (vision o karede boş döndüyse
        /// sonraki ilk dolu kare).
        /// </summary>
        public byte[]? Evidence { get; private set; }

        /// <summary>Türüne göre: en fazla yüz, en yüksek telefon güveni, en büyük |yaw|, en yüksek benzerlik.</summary>
        public double Max { get; set; }

        /// <summary>IDENTITY_MISMATCH: en düşük benzerlik.</summary>
        public double Min { get; set; }

        /// <summary>GAZE_AWAY: en büyük |pitch|.</summary>
        public double Second { get; set; }

        /// <summary>GAZE_AWAY: seriyi sürdüren ama sayılmayan "yüz yok" ölçümleri.</summary>
        public int Bridged { get; set; }

        public Run Extend(DateTimeOffset at, byte[]? evidence)
        {
            Count++;
            Last = at;
            Evidence ??= evidence;
            return this;
        }

        public CameraEpisode ToEpisode(EventType type, double? confidence, object payload) =>
            new(type, Start, (Last - Start).TotalSeconds, confidence, JsonSerializer.Serialize(payload), Evidence);
    }
}
