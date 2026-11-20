using AIGuard.Application.Sessions;

namespace AIGuard.Api.Sessions;

/// <summary>
/// <see cref="SessionTimekeeper"/>'ı birkaç saniyede bir çalıştırır (E4): süresi dolan oturumu otomatik
/// teslim, istek gelmeyen oturumu bağlantı kesik sayma. <c>Sessions:TimekeeperEnabled=false</c> ile kapatılabilir
/// (entegrasyon testleri sabit tarihli oturumlar kullandığı için kapatıyor).
/// </summary>
public sealed class SessionTimekeeperService(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<SessionTimekeeperService> logger)
    : BackgroundService
{
    public const string EnabledSetting = "Sessions:TimekeeperEnabled";
    public const string IntervalSetting = "Sessions:TimekeeperIntervalSeconds";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue(EnabledSetting, true))
        {
            return;
        }

        // Süre dolduktan en fazla birkaç saniye sonra teslim; cevap ucu zaten süre sonrası cevabı reddediyor.
        var seconds = configuration.GetValue(IntervalSetting, 5);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 30)));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var (completed, disconnected) = await scope.ServiceProvider.GetRequiredService<SessionTimekeeper>().RunOnceAsync(stoppingToken);
                if (completed + disconnected > 0)
                {
                    logger.LogInformation("{Completed} oturum otomatik teslim edildi, {Disconnected} oturum bağlantı kesik sayıldı.",
                        completed, disconnected);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Tek bir hatalı tur (ör. veritabanı kısa süre erişilemez) denetimi durdurmasın.
                logger.LogWarning(ex, "Oturum süre denetimi başarısız; bir sonraki turda tekrar denenecek.");
            }
        }
    }
}
