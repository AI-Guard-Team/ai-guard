using System.Text.RegularExpressions;
using AIGuard.Agent.Core.Platform;

namespace AIGuard.Agent.Windows;

/// <summary>
/// Ağ göstergeleri (Y4, FR-20): etkin VPN/TUN/TAP bağdaştırıcıları ve sistem vekil sunucu ayarı.
/// Bilinen VPN uygulamalarının süreçleri <c>forbidden-apps.json</c>'da (<c>"type": "VPN_PROXY"</c>).
/// </summary>
/// <remarks>
/// Hyper-V (vEthernet), WSL, Docker, VirtualBox Host-Only ve VMware VMnet bağdaştırıcıları VPN değil;
/// adlarında aranan kelimeler geçmediği için sayılmıyor. Kural yalnızca etkin (Up) bağdaştırıcılara bakıyor:
/// VPN uygulaması kurulu ama bağlı değilse bağdaştırıcısı kapalı oluyor.
/// </remarks>
internal static partial class NetworkDetector
{
    // Kısa kelimeler (TAP, TUN, VPN, Tunnel) yalnızca tam kelime olarak: "Adapter" içindeki "ap" ya da
    // "Tunneling" (Teredo) eşleşmesin. Marka adları olduğu gibi.
    [GeneratedRegex(
        @"\b(TAP|TUN|VPN|Tunnel)\b|Wintun|WireGuard|OpenVPN|NordLynx|AnyConnect|GlobalProtect|PANGP|Fortinet|FortiClient|Pulse Secure|Juniper|SonicWall|Cloudflare WARP|Windscribe|Surfshark|Mullvad|ProtonVPN|ExpressVPN",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VpnAdapterPattern();

    public static IReadOnlyList<DeviceIndicator> Detect(IWindowsSystemProbe probe)
    {
        var found = new List<DeviceIndicator>();

        foreach (var adapter in probe.ActiveNetworkAdapters())
        {
            // Windows'un kendi VPN bağlantıları (PPTP, L2TP, SSTP, IKEv2) PPP türünde görünüyor.
            if (adapter.Kind == "Ppp" || VpnAdapterPattern().IsMatch(adapter.Description) || VpnAdapterPattern().IsMatch(adapter.Name))
            {
                found.Add(new DeviceIndicator(DeviceIndicator.VpnProxy, "VPN bağdaştırıcısı",
                    DeviceIndicator.Truncate($"{adapter.Description} ({adapter.Name})")));
            }
        }

        var proxy = probe.ReadProxySettings();
        if (proxy.UserProxy is { } user)
        {
            found.Add(new DeviceIndicator(DeviceIndicator.VpnProxy, "Vekil sunucu (Windows ayarı)", DeviceIndicator.Truncate(user)));
        }

        if (proxy.UserPacUrl is { } pac)
        {
            found.Add(new DeviceIndicator(DeviceIndicator.VpnProxy, "Vekil sunucu (otomatik yapılandırma betiği)", DeviceIndicator.Truncate(pac)));
        }

        if (proxy.MachineProxy is { } machine)
        {
            found.Add(new DeviceIndicator(DeviceIndicator.VpnProxy, "Vekil sunucu (WinHTTP)", DeviceIndicator.Truncate(machine)));
        }

        // Sunucu göstergeleri tür + ada göre tekilleştiriyor; birden fazla VPN bağdaştırıcısı tek gösterge olsun.
        return found
            .GroupBy(i => i.Name)
            .Select(g => g.Count() == 1 ? g.First() : g.First() with
            {
                Detail = DeviceIndicator.Truncate(string.Join("; ", g.Select(i => i.Detail))),
            })
            .ToList();
    }
}
