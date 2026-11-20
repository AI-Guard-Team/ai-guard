using AIGuard.Agent.Core.Platform;

namespace AIGuard.Agent.MacOS;

/// <summary>macOS cihaz incelemesi. Y3 yalnızca Windows'u kapsıyor; macOS göstergeleri ayrı kartta, şimdilik boş liste.</summary>
internal sealed class MacDeviceInspector : IDeviceInspector
{
    public string Platform => "MacOS";

    public Task<IReadOnlyList<DeviceIndicator>> InspectAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DeviceIndicator>>([]);
}
