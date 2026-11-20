namespace AIGuard.Agent.Core.Platform;

/// <summary>
/// İşletim sistemine özgü cihaz incelemesi. Ortak çekirdek bu arayüzü kullanıyor; uygulaması
/// AIGuard.Agent.Windows ve AIGuard.Agent.MacOS projelerinde.
/// </summary>
/// <remarks>
/// <see cref="Signals.SignalSender"/> her sinyalden önce çağırıyor. Uygulama pahalı denetimleri
/// (WMI, süreç taraması) kendi aralığıyla yapmalı; ajanın ortalama işlemci kullanımı %5'in altında
/// kalmalı (GAB 3.3.3).
/// </remarks>
public interface IDeviceInspector
{
    /// <summary>
    /// İşletim sistemi adı: "Windows" veya "MacOS". Sunucudaki <c>AgentPlatform</c> enum'uyla
    /// aynı yazım (K-05); eşleştirmede sunucuya bu değer gidiyor.
    /// </summary>
    string Platform { get; }

    /// <summary>Yalnızca eşleşen göstergeler; tam süreç listesi değil (Y3, FR-19).</summary>
    Task<IReadOnlyList<DeviceIndicator>> InspectAsync(CancellationToken cancellationToken = default);
}
