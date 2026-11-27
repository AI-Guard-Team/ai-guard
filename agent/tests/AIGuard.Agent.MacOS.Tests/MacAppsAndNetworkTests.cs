using AIGuard.Agent.Core;
using AIGuard.Agent.Core.Platform;
using AIGuard.Agent.MacOS;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AIGuard.Agent.MacOS.Tests;

internal static class MacTestHost
{
    /// <summary>Ajanla birlikte yayımlanan gerçek liste; testler liste dosyasını da doğruluyor.</summary>
    public static readonly ForbiddenAppList Apps =
        ForbiddenAppList.Load(Path.Combine(AppContext.BaseDirectory, "forbidden-apps.json"));

    public static MacDeviceInspector CreateInspector(FakeProbe probe, FakeTimeProvider? clock = null, int scanSeconds = 15) =>
        new(probe, Apps, clock ?? new FakeTimeProvider(new DateTimeOffset(2026, 11, 26, 9, 0, 0, TimeSpan.Zero)),
            Options.Create(new AgentOptions { ProcessScanIntervalSeconds = scanSeconds }));
}

public class MacForbiddenAppListTests
{
    [Fact]
    public void Shipped_list_loads()
    {
        Assert.NotEmpty(MacTestHost.Apps.Apps);
    }

    [Theory]
    [InlineData("AnyDesk", "AnyDesk")]
    [InlineData("TeamViewer", "TeamViewer")]
    [InlineData("OBS", "OBS Studio (sanal kamera)")]
    [InlineData("zoom.us", "Zoom")]
    [InlineData("Microsoft Teams", "Microsoft Teams")]
    [InlineData("MSTeams", "Microsoft Teams")]
    [InlineData("Discord", "Discord")]
    public void Forbidden_apps_from_the_card_are_detected(string process, string name)
    {
        var indicator = Assert.Single(MacTestHost.Apps.Match([process]));

        Assert.Equal((DeviceIndicator.ForbiddenApp, name), (indicator.Type, indicator.Name));
    }

    [Theory]
    [InlineData("screensharingd")]
    [InlineData("ScreensharingAgent")]
    [InlineData("Screen Sharing")]
    [InlineData("ARDAgent")]
    public void Macos_screen_sharing_is_detected(string process)
    {
        var indicator = Assert.Single(MacTestHost.Apps.Match([process]));

        Assert.Equal((DeviceIndicator.ForbiddenApp, "macOS Ekran Paylaşımı"), (indicator.Type, indicator.Name));
    }

    [Theory]
    [InlineData("OpenVPN Connect", "OpenVPN")]
    [InlineData("WireGuard", "WireGuard")]
    [InlineData("NordVPN", "NordVPN")]
    [InlineData("Tunnelblick", "Tunnelblick")]
    [InlineData("Cisco AnyConnect Secure Mobility Client", "Cisco AnyConnect")]
    public void Known_vpn_apps_are_reported_as_vpn(string process, string name)
    {
        var indicator = Assert.Single(MacTestHost.Apps.Match([process]));

        Assert.Equal((DeviceIndicator.VpnProxy, name), (indicator.Type, indicator.Name));
    }

    [Fact]
    public void Process_names_ignore_case()
    {
        Assert.Equal("Discord", Assert.Single(MacTestHost.Apps.Match(["discord"])).Name);
    }

    [Fact]
    public void Ordinary_desktop_has_no_indicator()
    {
        // Çalışırken sık görülen macOS ve geliştirme süreçleri yanlış alarm vermemeli.
        string[] ordinary =
        [
            "launchd", "Finder", "Dock", "WindowServer", "Safari", "Google Chrome", "Google Chrome Helper", "Spotify",
            "Cursor", "Cursor Helper", "Code", "Terminal", "iTerm2", "Slack", "WhatsApp", "Mail", "Messages", "FaceTime",
            "Parallels Desktop", "prl_client_app", "prl_vm_app", "VMware Fusion", "UTM", "dotnet", "node", "docker",
            "Xcode", "Preview", "Notes", "CapCut",
        ];

        Assert.Empty(MacTestHost.Apps.Match(ordinary));
    }

    [Fact]
    public void Always_on_vpn_services_are_not_in_the_list()
    {
        // VPN kurulu ama bağlı değilken de çalışan servisler yanlış alarm verirdi.
        Assert.Empty(MacTestHost.Apps.Match(["warp-svc", "vpnagentd", "nordvpnd"]));
    }
}

public class MacNetworkDetectorTests
{
    [Fact]
    public void Wifi_without_vpn_has_no_indicator()
    {
        Assert.Empty(NetworkDetector.Detect(new FakeProbe()));
    }

    [Fact]
    public void Idle_macos_tunnel_interfaces_are_not_vpn()
    {
        // utun0..utun3 macOS'un kendisinin açtığı arayüzler (iCloud, Handoff). Trafiği taşımıyorlar: rota hâlâ en0.
        var probe = new FakeProbe { Routes = new() { ["default"] = "en0", [NetworkDetector.PublicProbeAddress] = "en0" } };

        Assert.Empty(NetworkDetector.Detect(probe));
    }

    [Fact]
    public void Default_route_through_a_tunnel_is_vpn()
    {
        var probe = new FakeProbe { Routes = new() { ["default"] = "utun4", [NetworkDetector.PublicProbeAddress] = "utun4" } };

        var indicator = Assert.Single(NetworkDetector.Detect(probe));

        Assert.Equal((DeviceIndicator.VpnProxy, "VPN bağdaştırıcısı"), (indicator.Type, indicator.Name));
        Assert.Contains("utun4", indicator.Detail);
    }

    [Fact]
    public void Split_default_vpn_is_found_through_the_public_route()
    {
        // WireGuard ve OpenVPN (def1) varsayılan rotayı silmeden 0.0.0.0/1 ve 128.0.0.0/1 ekliyor.
        var probe = new FakeProbe { Routes = new() { ["default"] = "en0", [NetworkDetector.PublicProbeAddress] = "utun5" } };

        var indicator = Assert.Single(NetworkDetector.Detect(probe));

        Assert.Equal("VPN bağdaştırıcısı", indicator.Name);
        Assert.Contains("utun5 (genel adrese giden rota)", indicator.Detail);
    }

    [Theory]
    [InlineData("ipsec0")]
    [InlineData("ppp0")]
    [InlineData("utun12")]
    [InlineData("tun0")]
    public void Tunnel_interface_kinds_are_vpn(string name)
    {
        var probe = new FakeProbe { Routes = new() { ["default"] = name, [NetworkDetector.PublicProbeAddress] = name } };

        Assert.Single(NetworkDetector.Detect(probe));
    }

    [Theory]
    [InlineData("en0")]
    [InlineData("en1")]
    [InlineData("bridge100")]
    [InlineData("lo0")]
    [InlineData("awdl0")]
    public void Ordinary_interfaces_are_not_vpn(string name)
    {
        // bridge100: Parallels/VMware'in sanal ağı; VPN değil.
        var probe = new FakeProbe { Routes = new() { ["default"] = name, [NetworkDetector.PublicProbeAddress] = name } };

        Assert.Empty(NetworkDetector.Detect(probe));
    }

    [Fact]
    public void Unreadable_routes_do_not_break_detection()
    {
        Assert.Empty(NetworkDetector.Detect(new FakeProbe { Routes = new() }));
    }

    [Fact]
    public void System_proxy_is_reported()
    {
        var probe = new FakeProbe { Proxy = new(Http: "proxy.kurum.local:8080", Https: "proxy.kurum.local:8080", Socks: "127.0.0.1:1080") };

        var indicator = Assert.Single(NetworkDetector.Detect(probe));

        Assert.Equal((DeviceIndicator.VpnProxy, "Vekil sunucu (macOS ayarı)"), (indicator.Type, indicator.Name));
        Assert.Equal("HTTP proxy.kurum.local:8080; HTTPS proxy.kurum.local:8080; SOCKS 127.0.0.1:1080", indicator.Detail);
    }

    [Fact]
    public void Proxy_auto_config_script_is_reported()
    {
        var probe = new FakeProbe { Proxy = new(PacUrl: "http://wpad.kurum.local/proxy.pac") };

        var indicator = Assert.Single(NetworkDetector.Detect(probe));

        Assert.Equal("Vekil sunucu (otomatik yapılandırma betiği)", indicator.Name);
    }

    [Fact]
    public void Vpn_and_proxy_are_both_reported()
    {
        var probe = new FakeProbe
        {
            Routes = new() { ["default"] = "utun4", [NetworkDetector.PublicProbeAddress] = "utun4" },
            Proxy = new(Http: "p:8080"),
        };

        Assert.Equal(2, NetworkDetector.Detect(probe).Count);
    }
}

public class MacDeviceInspectorFullTests
{
    [Fact]
    public async Task Physical_mac_without_vpn_gives_no_false_alarm()
    {
        var inspector = MacTestHost.CreateInspector(new FakeProbe());

        Assert.Empty(await inspector.InspectAsync());
    }

    [Fact]
    public async Task Forbidden_app_vpn_and_vm_are_reported_together_in_the_shared_format()
    {
        var probe = new FakeProbe
        {
            Hardware = new(HypervisorPresent: true, ModelName: "Apple Virtual Machine 1", ModelIdentifier: "VirtualMac2,1"),
            Processes = ["Finder", "AnyDesk", "Discord"],
            Routes = new() { ["default"] = "utun4", [NetworkDetector.PublicProbeAddress] = "utun4" },
        };

        var result = await MacTestHost.CreateInspector(probe).InspectAsync();

        Assert.Equal(
            [DeviceIndicator.VmDetected, DeviceIndicator.ForbiddenApp, DeviceIndicator.ForbiddenApp, DeviceIndicator.VpnProxy],
            result.Select(i => i.Type).ToArray());
        Assert.All(result, i => Assert.True(i.Name.Length <= DeviceIndicator.MaxNameLength));
        Assert.All(result, i => Assert.True((i.Detail ?? "").Length <= DeviceIndicator.MaxDetailLength));
    }

    [Fact]
    public async Task Only_matching_names_are_sent_never_the_process_list()
    {
        var probe = new FakeProbe { Processes = ["Finder", "Safari", "AnyDesk", "gizli-belge-duzenleyici"] };

        var result = await MacTestHost.CreateInspector(probe).InspectAsync();

        var indicator = Assert.Single(result);
        Assert.Equal("AnyDesk", indicator.Name);
        Assert.DoesNotContain(result, i => (i.Detail ?? "").Contains("Safari") || (i.Detail ?? "").Contains("gizli"));
    }

    [Fact]
    public async Task Processes_are_scanned_at_the_interval_not_on_every_signal()
    {
        var probe = new FakeProbe();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 26, 9, 0, 0, TimeSpan.Zero));
        var inspector = MacTestHost.CreateInspector(probe, clock, scanSeconds: 15);

        await inspector.InspectAsync();
        clock.Advance(TimeSpan.FromSeconds(5));
        await inspector.InspectAsync();
        clock.Advance(TimeSpan.FromSeconds(5));
        await inspector.InspectAsync();
        Assert.Equal(1, probe.ProcessScans);

        clock.Advance(TimeSpan.FromSeconds(10));
        await inspector.InspectAsync();
        Assert.Equal(2, probe.ProcessScans);
    }

    [Fact]
    public async Task A_closed_app_disappears_at_the_next_scan()
    {
        var probe = new FakeProbe { Processes = ["Finder", "Discord"] };
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 26, 9, 0, 0, TimeSpan.Zero));
        var inspector = MacTestHost.CreateInspector(probe, clock);

        Assert.Single(await inspector.InspectAsync());

        probe.Processes = ["Finder"];
        clock.Advance(TimeSpan.FromSeconds(16));

        Assert.Empty(await inspector.InspectAsync());
    }
}

public class MacCommandOutputTests
{
    private const string Ps = "/sbin/launchd\n/Applications/zoom.us.app/Contents/MacOS/zoom.us\n"
        + "/Applications/Cisco/Cisco AnyConnect Secure Mobility Client.app/Contents/MacOS/Cisco AnyConnect Secure Mobility Client\n"
        + "/usr/sbin/cfprefsd\n/sbin/launchd\n";

    [Fact]
    public void Process_names_are_the_file_names_without_duplicates()
    {
        var names = MacSystemProbe.ParseProcessNames(Ps);

        Assert.Equal(["launchd", "zoom.us", "Cisco AnyConnect Secure Mobility Client", "cfprefsd"], names);
    }

    [Fact]
    public void Empty_process_output_gives_an_empty_list()
    {
        Assert.Empty(MacSystemProbe.ParseProcessNames(null));
        Assert.Empty(MacSystemProbe.ParseProcessNames(""));
    }

    [Theory]
    [InlineData("   route to: 1.1.1.1\ndestination: default\n       mask: default\n    gateway: 192.168.1.1\n  interface: en0\n      flags: <UP,GATEWAY,DONE,STATIC,PRCLONING,GLOBAL>", "en0")]
    [InlineData("   route to: 1.1.1.1\ndestination: 0.0.0.0\n       mask: 128.0.0.0\n    gateway: 10.0.0.1\n  interface: utun5\n", "utun5")]
    public void Route_interface_is_read(string output, string expected)
    {
        Assert.Equal(expected, MacSystemProbe.ParseRouteInterface(output));
    }

    [Fact]
    public void Route_output_without_an_interface_gives_null()
    {
        Assert.Null(MacSystemProbe.ParseRouteInterface("route: writing to routing socket: not in table"));
        Assert.Null(MacSystemProbe.ParseRouteInterface(null));
    }

    private const string NoProxy = """
        <dictionary> {
          ExceptionsList : <array> {
            0 : *.local
            1 : 169.254/16
          }
          FTPPassive : 1
          HTTPEnable : 0
          HTTPSEnable : 0
          SOCKSEnable : 0
        }
        """;

    private const string WithProxy = """
        <dictionary> {
          ExceptionsList : <array> {
            0 : *.local
          }
          HTTPEnable : 1
          HTTPPort : 8080
          HTTPProxy : proxy.kurum.local
          HTTPSEnable : 1
          HTTPSPort : 8443
          HTTPSProxy : proxy.kurum.local
          ProxyAutoConfigEnable : 1
          ProxyAutoConfigURLString : http://wpad.kurum.local/proxy.pac
          SOCKSEnable : 1
          SOCKSPort : 1080
          SOCKSProxy : 127.0.0.1
        }
        """;

    [Fact]
    public void No_proxy_gives_empty_settings()
    {
        Assert.Equal(new MacProxySettings(), MacSystemProbe.ParseProxySettings(NoProxy));
        Assert.Equal(new MacProxySettings(), MacSystemProbe.ParseProxySettings(null));
    }

    [Fact]
    public void Enabled_proxies_are_read()
    {
        var proxy = MacSystemProbe.ParseProxySettings(WithProxy);

        Assert.Equal("proxy.kurum.local:8080", proxy.Http);
        Assert.Equal("proxy.kurum.local:8443", proxy.Https);
        Assert.Equal("127.0.0.1:1080", proxy.Socks);
        Assert.Equal("http://wpad.kurum.local/proxy.pac", proxy.PacUrl);
    }

    [Fact]
    public void Disabled_proxy_with_leftover_host_is_ignored()
    {
        var proxy = MacSystemProbe.ParseProxySettings("""
            <dictionary> {
              HTTPEnable : 0
              HTTPPort : 8080
              HTTPProxy : eski.kurum.local
            }
            """);

        Assert.Null(proxy.Http);
    }

    [Fact]
    public void Scoped_per_interface_entries_are_not_counted()
    {
        // __SCOPED__ altında arayüze özel iç içe kayıtlar var; üst düzey ayar kapalıysa gösterge çıkmamalı.
        var proxy = MacSystemProbe.ParseProxySettings("""
            <dictionary> {
              HTTPEnable : 0
              __SCOPED__ : <dictionary> {
                en0 : <dictionary> {
                  HTTPEnable : 1
                  HTTPPort : 3128
                  HTTPProxy : eski.kurum.local
                }
              }
            }
            """);

        Assert.Equal(new MacProxySettings(), proxy);
    }

    [Fact]
    public void Live_macos_reads_do_not_throw()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var probe = new MacSystemProbe(NullLogger<MacSystemProbe>.Instance);

        Assert.NotEmpty(probe.RunningProcessNames());
        _ = probe.RouteInterface("default");
        _ = probe.ReadProxySettings();
    }
}
