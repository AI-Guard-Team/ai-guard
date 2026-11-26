using System.Collections.Concurrent;
using System.Text.Json;
using AIGuard.Domain.Enums;

namespace AIGuard.Application.Monitoring;

/// <summary>
/// Bir oturumun kamera izleme durumu; yalnızca bellekte (API tek örnek, S-17). Alanlara <see cref="Sync"/>
/// kilidi altında erişilmeli; kilit görüntü servisi çağrısı ve veritabanı yazımı sırasında tutulmuyor
/// (teslim oturum satırını kilitleyip bu kilidi bekleyebildiği için, K-45).
/// </summary>
public sealed class CameraSessionState(CameraMonitorOptions options, DateTimeOffset createdAt)
{
    public object Sync { get; } = new();

    public CameraEpisodeTracker Tracker { get; } = new(options);

    /// <summary>Son kare isteğinin anı (sonucu ne olursa olsun); boşta silme buna bakıyor.</summary>
    public DateTimeOffset LastActivityAt { get; set; } = createdAt;

    /// <summary>Son gerçek telefon ölçümünün anı; yalnızca phone sonucu gelince güncelleniyor.</summary>
    public DateTimeOffset? LastPhoneAt { get; set; }

    /// <summary>Son gerçek kimlik ölçümünün anı; yalnızca matched true/false gelince güncelleniyor.</summary>
    public DateTimeOffset? LastIdentityAt { get; set; }

    /// <summary>Telefon kontrolü isteyen bir istek yolda; aynı anda gelen kare aynı kontrolü tekrar istemiyor.</summary>
    public bool PhoneInFlight { get; set; }

    public bool IdentityInFlight { get; set; }

    /// <summary>Adayın referans vektörü: oturumda bir kez çözülüyor, yalnızca bellekte; oturum bitince siliniyor.</summary>
    public float[]? Reference { get; set; }

    /// <summary>Vektör yüklenemedi (şablon yok ya da çözülemedi); bu oturumda kimlik kontrolü yapılmıyor.</summary>
    public bool ReferenceUnavailable { get; set; }

    /// <summary>Oturum kapandı; geç gelen sonuçlar atılıyor, durum yeniden oluşturulmuyor.</summary>
    public bool Closed { get; private set; }

    public MonitoringGap? Gap { get; set; }

    /// <summary>Oturumu kapatır ve vektörü bellekten siler.</summary>
    public void Close()
    {
        Closed = true;
        WipeReference();
    }

    public void WipeReference()
    {
        if (Reference is not null)
        {
            Array.Clear(Reference);
            Reference = null;
        }
    }
}

/// <summary>Görüntü servisinin yanıt vermediği aralık: ilk başarısız kareden itibaren tek bölüm (K-45).</summary>
public sealed class MonitoringGap(DateTimeOffset startedAt, int? httpStatus, string? code)
{
    public DateTimeOffset StartedAt { get; } = startedAt;

    public DateTimeOffset LastFailureAt { get; private set; } = startedAt;

    public int FailedFrames { get; private set; } = 1;

    /// <summary>İlk hatanın durumu ve kodu; değerlendirici boşluğun nedenini görsün.</summary>
    public int? HttpStatus { get; } = httpStatus;

    public string? Code { get; } = code;

    public void Fail(DateTimeOffset at)
    {
        FailedFrames++;
        LastFailureAt = at;
    }

    /// <param name="end">İlk başarılı yanıt, ya da kareler kesildiyse / oturum bittiyse son başarısız kare.</param>
    public CameraEpisode ToEpisode(DateTimeOffset end, string closedBy) => new(
        EventType.MONITORING_GAP,
        StartedAt,
        Math.Max(0, (end - StartedAt).TotalSeconds),
        null,
        JsonSerializer.Serialize(new { component = "camera", failedFrames = FailedFrames, status = HttpStatus, code = Code, closedBy }),
        null);
}

/// <summary>Oturum başına kamera izleme durumları; uygulama boyunca tek (singleton).</summary>
public sealed class CameraMonitorStore
{
    private readonly ConcurrentDictionary<Guid, CameraSessionState> _sessions = new();

    public int Count => _sessions.Count;

    public CameraSessionState GetOrAdd(Guid sessionId, CameraMonitorOptions options, DateTimeOffset now) =>
        _sessions.GetOrAdd(sessionId, _ => new CameraSessionState(options, now));

    public bool TryGet(Guid sessionId, out CameraSessionState state) => _sessions.TryGetValue(sessionId, out state!);

    public IReadOnlyList<KeyValuePair<Guid, CameraSessionState>> Snapshot() => _sessions.ToArray();

    /// <summary>Yalnızca verilen durum hâlâ kayıtlıysa siler (arada yenisi oluşturulduysa ona dokunmaz).</summary>
    public void Remove(Guid sessionId, CameraSessionState state)
    {
        if (_sessions.TryRemove(new KeyValuePair<Guid, CameraSessionState>(sessionId, state)))
        {
            lock (state.Sync)
            {
                state.Close();
            }
        }
    }

    /// <summary>Durumu siler; vektör bellekten temizleniyor, yolda olan istek sonucunu atıyor.</summary>
    public void Remove(Guid sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var state))
        {
            lock (state.Sync)
            {
                state.Close();
            }
        }
    }
}
