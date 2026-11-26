using System.Collections.Concurrent;

namespace AIGuard.Application.Sessions;

public enum MediaKind
{
    Frame,
    Audio,
}

/// <summary>
/// Oturum ve tür başına kare/ses hız sınırı (S4). Önyüz saniyede bir gönderiyor; istemciye güvenilmediği için
/// sunucu da daha sık gelenleri reddediyor. Her saniye veritabanına yazmamak için bellekte tutuluyor; API tek
/// örnekle çalışıyor (S-17 ile aynı varsayım).
/// </summary>
public sealed class SessionMediaRateLimiter : ICameraPresence
{
    /// <summary>
    /// Soruların verilmesi için son kabul edilen karenin en fazla bu kadar eski olması gerekiyor (S-29). Kareler
    /// saniyede bir geliyor; 15 sn yavaş bağlantıda atlanan birkaç kareye ve önyüzün 10 sn'lik yoklamasına pay
    /// bırakıyor.
    /// </summary>
    public static readonly TimeSpan QuestionFrameWindow = TimeSpan.FromSeconds(15);

    /// <summary>
    /// İki parça arasındaki en kısa aralık; daha sık gelen parça reddediliyor (en fazla saniyede 4).
    /// Önyüz saniyede bir gönderiyor, ama ağ gecikmesi değişince iki kare sunucuya 500 ms'den yakın varabiliyor;
    /// tarayıcı denemesinde 500 ms'lik sınır böyle meşru kareleri düşürdü. 250 ms bu oynamaya pay bırakıyor.
    /// </summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(250);

    // Bu kadar eski kayıtlar ara sıra siliniyor; biten oturumlar bellekte birikmesin.
    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(1);
    private const int PruneEvery = 1024;

    private readonly ConcurrentDictionary<(Guid SessionId, MediaKind Kind), DateTimeOffset> _last = new();
    private int _calls;

    public bool TryAcquire(Guid sessionId, MediaKind kind, DateTimeOffset now)
    {
        if (Interlocked.Increment(ref _calls) % PruneEvery == 0)
        {
            Prune(now);
        }

        var key = (sessionId, kind);
        while (true)
        {
            if (!_last.TryGetValue(key, out var previous))
            {
                if (_last.TryAdd(key, now))
                {
                    return true;
                }

                continue;
            }

            if (now - previous < MinInterval)
            {
                return false;
            }

            // Aynı anda iki istek gelirse yalnızca biri güncelleyebiliyor; diğeri yeni değeri görüp reddediliyor.
            if (_last.TryUpdate(key, now, previous))
            {
                return true;
            }
        }
    }

    /// <summary>
    /// Kabul edilen son kare <see cref="QuestionFrameWindow"/> içindeyse true. Bellekteki kayıt kullanılıyor; API
    /// yeniden başlarsa kayıt sıfırlanır, ama kareler saniyede bir geldiği için sorular birkaç saniyede geri gelir.
    /// </summary>
    public bool HasRecentFrame(Guid sessionId, DateTimeOffset now) =>
        _last.TryGetValue((sessionId, MediaKind.Frame), out var at) && now - at <= QuestionFrameWindow && now >= at;

    private void Prune(DateTimeOffset now)
    {
        foreach (var (key, at) in _last)
        {
            if (now - at > Expiry)
            {
                _last.TryRemove(new KeyValuePair<(Guid, MediaKind), DateTimeOffset>(key, at));
            }
        }
    }
}
