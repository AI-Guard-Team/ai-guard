using AIGuard.Agent.Core;
using AIGuard.Agent.Core.Platform;
using Microsoft.Extensions.Options;

namespace AIGuard.Agent.MacOS;

/// <summary>
/// macOS cihaz ve ağ incelemesi (Y5, S6; FR-18, FR-19, FR-20). Kullanıcı modunda çalışıyor; yönetici izni istemiyor.
/// Sinyal biçimi Windows ile birebir aynı (<see cref="DeviceIndicator"/>).
/// </summary>
/// <remarks>
/// Denetimler işlemciyi yormasın diye (GAB 3.3.3, ortalama %5'in altı) farklı sıklıkta:
/// <list type="bullet">
/// <item>Sanal makine: ilk incelemede bir kez (sınav boyunca değişmiyor).</item>
/// <item>Süreç listesi, VPN rotası ve vekil sunucu ayarı: <c>Agent:ProcessScanIntervalSeconds</c> aralıkla
/// (varsayılan 15 sn); arada son sonuç kullanılıyor.</item>
/// </list>
/// </remarks>
internal sealed class MacDeviceInspector(
    IMacSystemProbe probe,
    ForbiddenAppList forbiddenApps,
    TimeProvider clock,
    IOptions<AgentOptions> options) : IDeviceInspector
{
    private readonly object _gate = new();
    private IReadOnlyList<DeviceIndicator>? _machine;
    private IReadOnlyList<DeviceIndicator> _apps = [];
    private DateTimeOffset? _lastProcessScan;

    public string Platform => "MacOS";

    public Task<IReadOnlyList<DeviceIndicator>> InspectAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _machine ??= MachineDetector.Detect(probe);

            var now = clock.GetUtcNow();
            var interval = TimeSpan.FromSeconds(options.Value.ProcessScanIntervalSeconds);
            if (_lastProcessScan is not { } last || now - last >= interval)
            {
                // Süreç adları yalnızca burada, listeyle karşılaştırmak için kullanılıyor; saklanmıyor, gönderilmiyor.
                _apps = [.. forbiddenApps.Match(probe.RunningProcessNames()), .. NetworkDetector.Detect(probe)];
                _lastProcessScan = now;
            }

            var result = new List<DeviceIndicator>(_machine);
            result.AddRange(_apps);
            return Task.FromResult<IReadOnlyList<DeviceIndicator>>(result);
        }
    }
}
