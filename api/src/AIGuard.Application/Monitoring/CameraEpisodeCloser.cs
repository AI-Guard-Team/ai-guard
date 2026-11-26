using AIGuard.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIGuard.Application.Monitoring;

/// <summary>
/// Kamera bölümlerinin kare akışı dışında kapanması (K-45): oturum bitince (<see cref="CloseSessionAsync"/>) ve
/// birkaç saniyede bir temizlik turunda (<see cref="SweepAsync"/>). Görüntü servisine bağlı değil; vision
/// ayarlanmamış ortamda da teslim ve temizlik çalışıyor.
/// </summary>
/// <remarks>
/// Depolama (kanıt deposu ve şifreleyici) yalnızca yazılacak ya da sorulacak bir oturum varken çözümleniyor: şifreleme
/// anahtarı olmayan bir bilgisayarda teslim, kamera izlenmemiş oturumlar için eskisi gibi çalışsın (K-26).
/// </remarks>
public sealed class CameraEpisodeCloser(
    CameraMonitorStore store,
    Func<ICameraMonitorStorage> storage,
    IOptions<CameraMonitorOptions> options,
    TimeProvider clock,
    ILogger<CameraEpisodeCloser> logger) : ISessionMonitoringCloser
{
    /// <summary>
    /// Teslim ve otomatik teslimde açık bölümleri son ölçümlerine göre yazar, durumu kapatır ve vektörü siler.
    /// Oturum satırı kilitliyken, işlem içinde ve durum değişmeden önce çağrılmalı (SessionService.CompleteAsync).
    /// </summary>
    public async Task CloseSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (!store.TryGet(sessionId, out var state))
        {
            return;
        }

        List<CameraEpisode> episodes;
        lock (state.Sync)
        {
            if (state.Closed)
            {
                return;
            }

            episodes = [.. state.Tracker.CloseAll(EpisodeEnd.SessionEnd)];
            if (state.Gap is { } gap)
            {
                episodes.Add(gap.ToEpisode(gap.LastFailureAt, EpisodeEnd.SessionEnd));
                state.Gap = null;
            }

            // Kapalı işaretli durum temizlik turuna kadar kalıyor; yolda olan karenin sonucu atılıyor.
            state.Close();
        }

        // Çağıranın işleminde yazılıyor; oturum henüz kapanmadığı için olay servisi kabul ediyor.
        if (episodes.Count > 0)
        {
            await CameraEpisodeWriter.WriteAsync(store, storage(), logger, sessionId, episodes, ct);
        }
    }

    /// <summary>
    /// Temizlik turu: kapanmış ya da iptal edilmiş oturumların durumu olay yazılmadan silinir; kareleri kesilen
    /// oturumun açık bölümleri yazılır; uzun süredir kare gelmeyen oturumun durumu silinir.
    /// </summary>
    public async Task SweepAsync(CancellationToken ct = default)
    {
        var sessions = store.Snapshot();
        if (sessions.Count == 0)
        {
            return;
        }

        // İptal (biyometrik rıza geri alındı): olay yazılmıyor, durum vektörle birlikte siliniyor (K-45).
        var data = storage();
        var ended = await data.FindEndedAsync(sessions.Select(s => s.Key).ToList(), ct);
        foreach (var id in ended)
        {
            store.Remove(id);
        }

        var o = options.Value;
        var now = clock.GetUtcNow();
        foreach (var (id, state) in sessions)
        {
            if (ended.Contains(id))
            {
                continue;
            }

            var episodes = new List<CameraEpisode>();
            bool remove;
            lock (state.Sync)
            {
                if (!state.Closed)
                {
                    if (state.Tracker.IsStale(now))
                    {
                        episodes.AddRange(state.Tracker.CloseAll(EpisodeEnd.Stale));
                    }

                    if (state.Gap is { } gap && now - gap.LastFailureAt >= o.StaleAfter)
                    {
                        episodes.Add(gap.ToEpisode(gap.LastFailureAt, EpisodeEnd.Stale));
                        state.Gap = null;
                    }
                }

                remove = now - state.LastActivityAt >= o.IdleRemoveAfter
                    && (state.Closed || (!state.Tracker.HasOpen && state.Gap is null && episodes.Count == 0));
                if (remove)
                {
                    // Kilit altında kapatılıyor: bu arada gelen kare durumu kapalı görüp sonucu atıyor.
                    state.Close();
                }
            }

            if (remove)
            {
                store.Remove(id, state);
                continue;
            }

            await CameraEpisodeWriter.WriteAsync(store, data, logger, id, episodes, ct);
        }
    }
}
