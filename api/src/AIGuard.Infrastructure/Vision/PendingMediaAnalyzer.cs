using AIGuard.Application.Abstractions;

namespace AIGuard.Infrastructure.Vision;

/// <summary>
/// D4/D5 gelene kadar sınav sırası kare ve ses parçasını analiz etmeden bırakan uygulama (S4). Görüntü servisinin
/// <c>/internal/analyze/frame</c> ve <c>/internal/analyze/audio</c> uçları henüz 503 dönüyor; onları her saniye
/// çağırmak MONITORING_GAP üretirdi. Veri hiçbir yere yazılmıyor, istek bitince bellekten çıkıyor.
/// </summary>
internal sealed class PendingMediaAnalyzer : IMediaAnalyzer
{
    public Task AnalyzeFrameAsync(Guid sessionId, byte[] image, DateTimeOffset receivedAt, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task AnalyzeAudioAsync(Guid sessionId, byte[] chunk, AudioFormat format, DateTimeOffset receivedAt, CancellationToken ct = default) =>
        Task.CompletedTask;
}
