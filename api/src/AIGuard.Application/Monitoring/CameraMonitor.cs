using AIGuard.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIGuard.Application.Monitoring;

/// <summary>
/// Sınav sırası kamera karesinin analizi (D4, K-45): görevleri zamanlar, görüntü servisini çağırır, ölçümü
/// olay kurallarına (<see cref="CameraEpisodeTracker"/>) verir, biten bölümleri ve MONITORING_GAP'i yazar.
/// </summary>
/// <remarks>
/// Çağrı istek içinde, senkron (S4'teki tarayıcı bir önceki kare gitmeden yenisini göndermiyor; zaman aşımı
/// <see cref="CameraMonitorOptions.VisionTimeoutSeconds"/>). Kare yalnızca bellekte; kanıt karesi ancak olay
/// yazılırsa saklanıyor. Ses D5'te; şimdilik hiçbir şey yapılmıyor.
/// Günlüğe yalnızca oturum kimliği, olay türü, HTTP durumu ve hata kodu yazılıyor; görüntü, vektör ve skor yok (K-27).
///
/// Depolama (kanıt deposu ve şifreleyici) <see cref="CameraEpisodeCloser"/>'daki gibi yalnızca yazılacak bölüm ya da
/// kimlik vektörü gerektiğinde çözümleniyor: şifreleme anahtarı olmayan bilgisayarda kare, ses ve izin uçları
/// (aynı controller) açılabilsin (K-26).
/// </remarks>
public sealed class CameraMonitor(
    CameraMonitorStore store,
    IFrameAnalysisService vision,
    Func<ICameraMonitorStorage> storage,
    IOptions<CameraMonitorOptions> options,
    ILogger<CameraMonitor> logger) : IMediaAnalyzer
{
    public async Task AnalyzeFrameAsync(Guid sessionId, byte[] image, DateTimeOffset receivedAt, CancellationToken ct = default)
    {
        var o = options.Value;
        var state = store.GetOrAdd(sessionId, o, receivedAt);

        bool phone, identity;
        lock (state.Sync)
        {
            if (state.Closed)
            {
                return;
            }

            state.LastActivityAt = receivedAt;

            // Son kontrol zamanı yalnızca gerçek ölçüm gelince ilerliyor; "yolda" işareti aynı kontrolün
            // eşzamanlı iki istekte birden istenmesini engelliyor.
            phone = !state.PhoneInFlight && (state.LastPhoneAt is not { } lastPhone || receivedAt - lastPhone >= o.PhoneInterval);
            identity = !state.ReferenceUnavailable && !state.IdentityInFlight
                && (state.LastIdentityAt is not { } lastIdentity || receivedAt - lastIdentity >= o.IdentityInterval);
            state.PhoneInFlight |= phone;
            state.IdentityInFlight |= identity;
        }

        try
        {
            var reference = identity ? await ReferenceAsync(sessionId, state, ct) : null;
            var result = await vision.AnalyzeAsync(sessionId, image, receivedAt, phone, reference, ct);

            var episodes = new List<CameraEpisode>();
            lock (state.Sync)
            {
                if (state.Closed)
                {
                    return;
                }

                switch (result.Status)
                {
                    case FrameAnalysisStatus.Ok:
                        Measured(sessionId, state, result.Measurement!, receivedAt, phone, reference is not null, episodes);
                        break;

                    case FrameAnalysisStatus.Unavailable:
                        Failed(sessionId, state, result, receivedAt, episodes);
                        break;

                    // INVALID_IMAGE adayın karesi: boşluk değil, bölümlere de dokunmuyor; kare atlanıyor.
                    case FrameAnalysisStatus.InvalidImage:
                        break;
                }
            }

            if (episodes.Count > 0)
            {
                await CameraEpisodeWriter.WriteAsync(store, storage(), logger, sessionId, episodes, CancellationToken.None);
            }
        }
        finally
        {
            lock (state.Sync)
            {
                state.PhoneInFlight &= !phone;
                state.IdentityInFlight &= !identity;
            }
        }
    }

    // Ses D5'te (konuşma tespiti); bu parçada analiz edilmiyor.
    public Task AnalyzeAudioAsync(Guid sessionId, byte[] chunk, AudioFormat format, DateTimeOffset receivedAt, CancellationToken ct = default) =>
        Task.CompletedTask;

    private void Measured(
        Guid sessionId, CameraSessionState state, FrameMeasurement m, DateTimeOffset at, bool phone, bool identity, List<CameraEpisode> episodes)
    {
        var o = options.Value;
        if (state.Gap is { } gap)
        {
            // Kareler arada kesildiyse boşluk son başarısız karede bitmiş sayılıyor (kesinti CONNECTION_LOST'un işi).
            var stale = at - gap.LastFailureAt >= o.StaleAfter;
            episodes.Add(gap.ToEpisode(stale ? gap.LastFailureAt : at, stale ? EpisodeEnd.Stale : EpisodeEnd.Recovered));
            state.Gap = null;
            logger.LogInformation("Kamera analizi yeniden çalışıyor: oturum {SessionId}", sessionId);
        }

        // Uzun aradan sonra gelen ölçüm eski serileri sürdürmüyor.
        if (state.Tracker.IsStale(at))
        {
            episodes.AddRange(state.Tracker.CloseAll(EpisodeEnd.Stale));
        }

        if (phone && m.Phone is not null)
        {
            state.LastPhoneAt = at;
        }

        if (identity && m.Identity is not null)
        {
            state.LastIdentityAt = at;
        }

        episodes.AddRange(state.Tracker.Apply(at, m));
    }

    private void Failed(Guid sessionId, CameraSessionState state, FrameAnalysisResult result, DateTimeOffset at, List<CameraEpisode> episodes)
    {
        if (state.Gap is { } gap && at - gap.LastFailureAt >= options.Value.StaleAfter)
        {
            // Kareler kesilip yeniden geldi, servis hâlâ yanıt vermiyor: önceki boşluk kapanıp yenisi açılıyor.
            episodes.Add(gap.ToEpisode(gap.LastFailureAt, EpisodeEnd.Stale));
            state.Gap = null;
        }

        if (state.Gap is null)
        {
            // Her kare için değil, boşluk başına bir uyarı.
            state.Gap = new MonitoringGap(at, result.HttpStatus, result.Code);
            logger.LogWarning("Kamera analizi yapılamıyor: oturum {SessionId}, HTTP {Status}, kod {Code}",
                sessionId, result.HttpStatus?.ToString() ?? "(yanıt yok)", result.Code ?? "(yok)");
        }
        else
        {
            state.Gap.Fail(at);
        }
    }

    /// <summary>Vektör oturumda bir kez çözülüyor; yalnızca kimlik kontrolünü üstlenen istek buraya giriyor.</summary>
    private async Task<float[]?> ReferenceAsync(Guid sessionId, CameraSessionState state, CancellationToken ct)
    {
        lock (state.Sync)
        {
            if (state.Reference is not null)
            {
                return state.Reference;
            }
        }

        ICameraMonitorStorage data;
        try
        {
            data = storage();
        }
        catch (InvalidOperationException)
        {
            // Şifreleme anahtarı tanımlı değil (K-26): vektör çözülemez, oturumda kimlik kontrolü yapılmıyor.
            // Kare yine analiz ediliyor; aksi hâlde her kare 500 alırdı.
            lock (state.Sync)
            {
                state.ReferenceUnavailable = true;
            }

            logger.LogWarning("Kimlik kontrolü yapılamıyor, kanıt deposu ya da şifreleyici kullanılamıyor: oturum {SessionId}", sessionId);
            return null;
        }

        var vector = await data.LoadReferenceVectorAsync(sessionId, ct);
        lock (state.Sync)
        {
            if (state.Closed)
            {
                if (vector is not null)
                {
                    Array.Clear(vector);
                }

                return null;
            }

            if (vector is null)
            {
                state.ReferenceUnavailable = true;
                logger.LogWarning("Kimlik kontrolü yapılamıyor, referans vektör yok ya da çözülemedi: oturum {SessionId}", sessionId);
                return null;
            }

            state.Reference = vector;
            return vector;
        }
    }
}

/// <summary>Bölümleri sırayla yazar; oturum kapanmışsa durumu siler.</summary>
internal static class CameraEpisodeWriter
{
    public static async Task WriteAsync(
        CameraMonitorStore store, ICameraMonitorStorage storage, ILogger logger, Guid sessionId,
        IReadOnlyList<CameraEpisode> episodes, CancellationToken ct)
    {
        foreach (var episode in episodes)
        {
            var status = await storage.RecordAsync(sessionId, episode, ct);
            switch (status)
            {
                case EpisodeWriteStatus.Recorded:
                    logger.LogInformation("Kamera olayı yazıldı: oturum {SessionId}, {Type}", sessionId, episode.Type);
                    break;

                case EpisodeWriteStatus.SessionClosed:
                    // Oturum arada kapandı (ör. iptal); kalan bölümler de yazılamaz.
                    store.Remove(sessionId);
                    return;

                default:
                    logger.LogWarning("Kamera olayı yazılamadı: oturum {SessionId}, {Type}", sessionId, episode.Type);
                    break;
            }
        }
    }
}
