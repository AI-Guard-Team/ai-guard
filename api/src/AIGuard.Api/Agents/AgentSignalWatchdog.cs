using AIGuard.Application.Agents;

namespace AIGuard.Api.Agents;

/// <summary>
/// <see cref="AgentSignalMonitor"/>'ü birkaç saniyede bir çalıştırır (Y2): 30 sn sinyal gelmeyen
/// oturumlara AGENT_SIGNAL_LOST, biten oturumların anahtarlarını geçersiz kılma.
/// </summary>
public sealed class AgentSignalWatchdog(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<AgentSignalWatchdog> logger)
    : BackgroundService
{
    public const string IntervalSetting = "Agent:WatchdogIntervalSeconds";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 30 sn'lik sınırın en fazla birkaç saniye geç fark edilmesi için kısa aralık.
        var seconds = configuration.GetValue(IntervalSetting, 5);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 30)));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var lost = await scope.ServiceProvider.GetRequiredService<AgentSignalMonitor>().RunOnceAsync(stoppingToken);
                if (lost > 0)
                {
                    logger.LogInformation("{Count} oturum için ajan sinyali kesildi olayı yazıldı.", lost);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Tek bir hatalı tur (ör. veritabanı kısa süre erişilemez) izlemeyi durdurmasın.
                logger.LogWarning(ex, "Ajan sinyal denetimi başarısız; bir sonraki turda tekrar denenecek.");
            }
        }
    }
}
