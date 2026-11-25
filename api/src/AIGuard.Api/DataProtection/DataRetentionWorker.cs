using AIGuard.Application.DataProtection;

namespace AIGuard.Api.DataProtection;

/// <summary>
/// <see cref="DataRetentionService"/>'i günde bir çalıştırır (A5, FR-35, K-32). İlk tur açılıştan kısa süre sonra:
/// API sık yeniden başlatılsa da silme günlerce ertelenmesin. <c>Retention:Enabled=false</c> ile kapatılabilir
/// (entegrasyon testleri sabit tarihli veri kullandığı için kapatıyor).
/// </summary>
public sealed class DataRetentionWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<DataRetentionWorker> logger)
    : BackgroundService
{
    public const string EnabledSetting = "Retention:Enabled";
    public const string IntervalSetting = "Retention:IntervalHours";

    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue(EnabledSetting, true))
        {
            return;
        }

        var hours = Math.Clamp(configuration.GetValue(IntervalSetting, 24), 1, 24);
        await Task.Delay(StartupDelay, stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(hours));

        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<DataRetentionService>().RunOnceAsync(stoppingToken);
                if (result.Any)
                {
                    logger.LogInformation(
                        "Saklama süresi dolan veriler silindi: {EvidenceFrames} kanıt karesi, {Sessions} oturumun izleme verisi, " +
                        "{FaceTemplates} yüz vektörü, {ConsentRecords} rıza kaydı, {AuditLogs} denetim kaydı.",
                        result.EvidenceFrames, result.Sessions, result.FaceTemplates, result.ConsentRecords, result.AuditLogs);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Silinemeyen veri bir sonraki turda yeniden deneniyor; hata görünür olsun diye uyarı düzeyinde.
                logger.LogWarning(ex, "Saklama süresi görevi başarısız; bir sonraki turda tekrar denenecek.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
