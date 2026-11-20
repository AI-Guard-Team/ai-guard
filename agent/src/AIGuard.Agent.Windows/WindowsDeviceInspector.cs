using AIGuard.Agent.Core;
using AIGuard.Agent.Core.Platform;
using Microsoft.Extensions.Options;

namespace AIGuard.Agent.Windows;

/// <summary>
/// Windows cihaz incelemesi (Y3, FR-18, FR-19). Kullanıcı modunda çalışıyor; yönetici izni istemiyor.
/// </summary>
/// <remarks>
/// Denetimler işlemciyi yormasın diye (GAB 3.3.3, ortalama %5'in altı) farklı sıklıkta:
/// <list type="bullet">
/// <item>Sanal makine ve hipervizör: ilk incelemede bir kez (sınav boyunca değişmiyor).</item>
/// <item>Süreç listesi: <c>Agent:ProcessScanIntervalSeconds</c> aralıkla (varsayılan 15 sn); arada son sonuç kullanılıyor.</item>
/// <item>Uzak Masaüstü oturumu: her sinyalde (tek bir sistem çağrısı).</item>
/// </list>
/// </remarks>
internal sealed class WindowsDeviceInspector(
    IWindowsSystemProbe probe,
    ForbiddenAppList forbiddenApps,
    TimeProvider clock,
    IOptions<AgentOptions> options) : IDeviceInspector
{
    /// <summary>
    /// Adayın bilgisayarına dışarıdan Uzak Masaüstü ile bağlanılmış. mstsc.exe bunu göstermez (o yalnızca
    /// adayın başka bir bilgisayara bağlandığını gösterir); bu yüzden ayrı denetleniyor.
    /// </summary>
    internal static readonly DeviceIndicator IncomingRemoteDesktop =
        new(DeviceIndicator.ForbiddenApp, "Uzak Masaüstü oturumu", "gelen bağlantı (SM_REMOTESESSION)");

    private readonly object _gate = new();
    private IReadOnlyList<DeviceIndicator>? _machine;
    private IReadOnlyList<DeviceIndicator> _apps = [];
    private DateTimeOffset? _lastProcessScan;

    public string Platform => "Windows";

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
                _apps = forbiddenApps.Match(probe.RunningProcessNames());
                _lastProcessScan = now;
            }

            var result = new List<DeviceIndicator>(_machine);
            result.AddRange(_apps);
            if (probe.IsRemoteSession())
            {
                result.Add(IncomingRemoteDesktop);
            }

            return Task.FromResult<IReadOnlyList<DeviceIndicator>>(result);
        }
    }
}
