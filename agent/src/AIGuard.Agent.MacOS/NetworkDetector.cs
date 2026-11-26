using System.Text.RegularExpressions;
using AIGuard.Agent.Core.Platform;

namespace AIGuard.Agent.MacOS;

/// <summary>
/// Ağ göstergeleri (S6, FR-20): trafiği taşıyan VPN tüneli ve sistem vekil sunucu ayarı.
/// Bilinen VPN uygulamalarının süreçleri <c>forbidden-apps.json</c>'da (<c>"type": "VPN_PROXY"</c>).
/// </summary>
/// <remarks>
/// <para>
/// macOS, VPN olmasa da <c>utun0</c>…<c>utun3</c> gibi tünel arayüzlerini kendisi açıyor (iCloud, Handoff,
/// Ağ Uzantıları). Bu yüzden "utun var mı" diye bakılmıyor; <b>trafiğin gerçekten bir tünel arayüzünden çıkıp
/// çıkmadığına</b> bakılıyor: <c>route -n get default</c> ve genel bir adrese giden rota.
/// </para>
/// <para>
/// İkinci kontrol gerekli çünkü WireGuard ve OpenVPN (<c>redirect-gateway def1</c>) varsayılan rotayı silmiyor;
/// yerine <c>0.0.0.0/1</c> ve <c>128.0.0.0/1</c> ekliyor. <c>default</c> hâlâ <c>en0</c> görünür, ama genel bir
/// adrese giden rota tünele gider.
/// </para>
/// </remarks>
internal static partial class NetworkDetector
{
    /// <summary>Genel bir adres (Cloudflare DNS). Paket gönderilmiyor; yalnızca yönlendirme tablosuna soruluyor.</summary>
    internal const string PublicProbeAddress = "1.1.1.1";

    // utun: çoğu VPN (WireGuard, OpenVPN, Cisco, IKEv2), ipsec ve ppp: macOS'un kendi L2TP/IPsec bağlantıları,
    // tun ve tap: Tunnelblick/OpenVPN sürücüleri.
    [GeneratedRegex(@"^(utun|ipsec|ppp|tun|tap)\d*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TunnelInterfacePattern();

    public static IReadOnlyList<DeviceIndicator> Detect(IMacSystemProbe probe)
    {
        var found = new List<DeviceIndicator>();

        var routes = new List<string>();
        AddTunnelRoute(routes, probe.RouteInterface("default"), "varsayılan rota");
        AddTunnelRoute(routes, probe.RouteInterface(PublicProbeAddress), "genel adrese giden rota");
        if (routes.Count > 0)
        {
            found.Add(new DeviceIndicator(DeviceIndicator.VpnProxy, "VPN bağdaştırıcısı",
                DeviceIndicator.Truncate(string.Join("; ", routes))));
        }

        var proxy = probe.ReadProxySettings();
        var servers = new List<string>();
        if (proxy.Http is { } http)
        {
            servers.Add($"HTTP {http}");
        }

        if (proxy.Https is { } https)
        {
            servers.Add($"HTTPS {https}");
        }

        if (proxy.Socks is { } socks)
        {
            servers.Add($"SOCKS {socks}");
        }

        if (servers.Count > 0)
        {
            found.Add(new DeviceIndicator(DeviceIndicator.VpnProxy, "Vekil sunucu (macOS ayarı)",
                DeviceIndicator.Truncate(string.Join("; ", servers))));
        }

        if (proxy.PacUrl is { } pac)
        {
            found.Add(new DeviceIndicator(DeviceIndicator.VpnProxy, "Vekil sunucu (otomatik yapılandırma betiği)", DeviceIndicator.Truncate(pac)));
        }

        return found;
    }

    private static void AddTunnelRoute(List<string> routes, string? interfaceName, string label)
    {
        if (interfaceName is not null && TunnelInterfacePattern().IsMatch(interfaceName))
        {
            routes.Add($"{interfaceName} ({label})");
        }
    }
}
