using AIGuard.Agent.Core.Platform;

namespace AIGuard.Agent.MacOS;

/// <summary>
/// macOS cihaz incelemesi (Y5, FR-18). Kullanıcı modunda çalışıyor; yönetici izni istemiyor.
/// </summary>
/// <remarks>
/// Sanal makine sınav boyunca değişmediği için bir kez okunuyor. Yasaklı yazılım ve ağ
/// göstergeleri bu kartta yok (S6).
/// </remarks>
internal sealed class MacDeviceInspector(IMacSystemProbe probe) : IDeviceInspector
{
    private readonly object _gate = new();
    private IReadOnlyList<DeviceIndicator>? _machine;

    public string Platform => "MacOS";

    public Task<IReadOnlyList<DeviceIndicator>> InspectAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _machine ??= MachineDetector.Detect(probe);
            return Task.FromResult(_machine);
        }
    }
}
