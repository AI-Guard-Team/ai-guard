using AIGuard.Application.Monitoring;
using Microsoft.Extensions.Options;

namespace AIGuard.Api.Monitoring;

/// <summary>
/// Kamera izlemenin temizlik turunu (<see cref="CameraEpisodeCloser.SweepAsync"/>) birkaç saniyede bir çalıştırır
/// (D4, K-45): kareleri kesilen oturumun açık bölümlerini yazar, biten, iptal edilen ve boşta kalan oturumların
/// durumunu vektörle birlikte bellekten siler. <c>CameraMonitoring:SweepEnabled=false</c> ile kapatılabilir.
/// </summary>
public sealed class CameraMonitorSweepService(
    IServiceScopeFactory scopes, IOptions<CameraMonitorOptions> options, ILogger<CameraMonitorSweepService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        if (!o.SweepEnabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(o.SweepIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<CameraEpisodeCloser>().SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Tek bir hatalı tur (ör. veritabanı kısa süre erişilemez) temizliği durdurmasın.
                logger.LogWarning(ex, "Kamera izleme temizlik turu başarısız; bir sonraki turda tekrar denenecek.");
            }
        }
    }
}
