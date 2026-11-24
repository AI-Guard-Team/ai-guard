using AIGuard.Agent.Core.Pairing;
using AIGuard.Agent.Core.Platform;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIGuard.Agent.Core.Signals;

/// <summary>
/// Eşleşme kurulduktan sonra belirli aralıkla imzalı sinyal gönderir (Y2). Sunucu 30 sn sinyal
/// almazsa AGENT_SIGNAL_LOST yazıyor; aralık bunun epey altında olmalı (varsayılan 5 sn).
/// </summary>
internal sealed class SignalSender(
    PairingState pairing,
    IDeviceInspector inspector,
    ISignalClient client,
    TimeProvider clock,
    IOptions<AgentOptions> options,
    ILogger<SignalSender> logger) : BackgroundService
{
    // Ajan açık kaldığı sürece çalışan döngü: her aralıkta bir sinyal. Eşleşme yoksa tur boş geçiyor.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.SignalIntervalSeconds), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SendOnceAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Tek sinyal: (1) eşleşme ve oturum anahtarı var mı, (2) cihazı incele, (3) göstergelerle birlikte imzala,
    /// (4) gönder, (5) oturum bittiyse anahtarı unut.
    /// </summary>
    /// <returns>Gönderilmediyse (eşleşme yok) null.</returns>
    internal async Task<SignalSendResult?> SendOnceAsync(CancellationToken ct = default)
    {
        if (pairing.Current is not { SessionId: { } sessionId, SessionKey: { } key } current)
        {
            return null;
        }

        IReadOnlyList<DeviceIndicator> indicators;
        try
        {
            indicators = await inspector.InspectAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // İnceleme hatası sinyali durdurmasın; durursa sunucu "sinyal kesildi" sanar.
            logger.LogWarning(ex, "Cihaz incelemesi başarısız; göstergesiz sinyal gönderiliyor.");
            indicators = [];
        }

        // İmza oturum anahtarıyla HMAC-SHA256; anahtar yalnızca bellekte, sinyalde ve logda yok (Y2, K-21).
        var result = await client.SendAsync(SignalSigner.Create(key, sessionId, clock.GetUtcNow(), indicators), ct);

        if (result == SignalSendResult.SessionEnded)
        {
            // Kart: oturum bitince anahtar geçersiz. Ajan da unutuyor ve göndermeyi bırakıyor.
            pairing.Clear(current);
            logger.LogInformation("Oturum bitti; sinyal gönderimi durduruldu.");
        }

        return result;
    }
}
