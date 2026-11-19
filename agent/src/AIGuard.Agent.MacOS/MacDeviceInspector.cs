using AIGuard.Agent.Core.Platform;

namespace AIGuard.Agent.MacOS;

/// <summary>macOS cihaz incelemesi. Göstergeler Y3'te eklenecek; şimdilik boş liste.</summary>
internal sealed class MacDeviceInspector : IDeviceInspector
{
    public string Platform => "MacOS";

    public Task<IReadOnlyList<string>> InspectAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}
