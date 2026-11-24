namespace AIGuard.Application.Abstractions;

/// <summary>Sınav sırası ses parçasının biçimi; dosyanın ilk baytlarından belirleniyor (docs/contracts.md 6. bölüm).</summary>
public enum AudioFormat
{
    WebM,
    Wav,
}

/// <summary>
/// Sınav sırasında gelen kamera karesi ve ses parçasının analizi (S4 → D4, D5). Görüntü servisini çağırma, olay
/// kurallarını uygulama (S-20) ve MONITORING_GAP üretme bu arayüzün uygulamasında. Veri yalnızca bellekte
/// tutulmalı; kare ya da ses diske yazılmaz (kanıt karesi hariç, A5).
/// </summary>
public interface IMediaAnalyzer
{
    Task AnalyzeFrameAsync(Guid sessionId, byte[] image, DateTimeOffset receivedAt, CancellationToken ct = default);

    Task AnalyzeAudioAsync(Guid sessionId, byte[] chunk, AudioFormat format, DateTimeOffset receivedAt, CancellationToken ct = default);
}
