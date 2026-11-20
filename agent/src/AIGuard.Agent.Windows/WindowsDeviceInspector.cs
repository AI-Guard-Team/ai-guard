using AIGuard.Agent.Core.Platform;

namespace AIGuard.Agent.Windows;

/// <summary>Windows cihaz incelemesi. Göstergeler Y3'te eklenecek; şimdilik boş liste.</summary>
internal sealed class WindowsDeviceInspector : IDeviceInspector
{
    public string Platform => "Windows";

    public Task<IReadOnlyList<string>> InspectAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}
