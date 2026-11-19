namespace AIGuard.Agent.Core.Platform;

/// <summary>
/// İşletim sistemine özgü cihaz incelemesi. Ortak çekirdek bu arayüzü kullanıyor; uygulaması
/// AIGuard.Agent.Windows ve AIGuard.Agent.MacOS projelerinde.
/// </summary>
/// <remarks>
/// Y1'de yalnızca arayüz ve boş uygulamalar var. Sanal makine, uzak masaüstü, yasaklı uygulama
/// gibi göstergeler Y3'te eklenecek; dönüş tipi o kartta netleşebilir.
/// </remarks>
public interface IDeviceInspector
{
    /// <summary>Loglarda ve sinyallerde görünen platform adı ("windows", "macos").</summary>
    string Platform { get; }

    /// <summary>Bulunan göstergelerin adları; tam süreç listesi değil (Y3).</summary>
    Task<IReadOnlyList<string>> InspectAsync(CancellationToken cancellationToken = default);
}
