using AIGuard.Agent.Core;
using AIGuard.Agent.Core.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AIGuard.Agent.Windows.Tests;

/// <summary>Testin belirlediği bilgisayar: WMI değerleri, kayıt defteri anahtarları, servisler, süreçler.</summary>
internal sealed class FakeProbe : IWindowsSystemProbe
{
    public MachineInfo Machine { get; set; } = new("Dell Inc.", "Latitude 5440", HypervisorPresent: false, "Dell Inc.", "1.12.0", "ABC1234");
    public HashSet<string> Keys { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Services { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Processes { get; set; } = ["explorer", "chrome", "svchost"];
    public bool RemoteSession { get; set; }
    public int MachineReads { get; private set; }
    public int ProcessScans { get; private set; }

    public MachineInfo ReadMachineInfo()
    {
        MachineReads++;
        return Machine;
    }

    public bool LocalMachineKeyExists(string path) => Keys.Contains(path);
    public bool ServiceExists(string name) => Services.Contains(name);

    public IReadOnlyList<string> RunningProcessNames()
    {
        ProcessScans++;
        return Processes;
    }

    public bool IsRemoteSession() => RemoteSession;

    public List<NetworkAdapter> Adapters { get; set; } =
    [
        new("Wi-Fi", "MediaTek Wi-Fi 6 802.11ax PCIe Adapter", "Wireless80211"),
        new("vEthernet (WSL (Hyper-V firewall))", "Hyper-V Virtual Ethernet Adapter", "Ethernet"),
    ];

    public ProxySettings Proxy { get; set; } = new();

    public IReadOnlyList<NetworkAdapter> ActiveNetworkAdapters() => Adapters;

    public ProxySettings ReadProxySettings() => Proxy;
}

public class MachineDetectorTests
{
    [Fact]
    public void Ordinary_physical_computer_has_no_indicator()
    {
        Assert.Empty(MachineDetector.Detect(new FakeProbe()));
    }

    [Fact]
    public void VirtualBox_guest_is_detected_from_wmi_bios_registry_and_drivers()
    {
        var probe = new FakeProbe
        {
            Machine = new("innotek GmbH", "VirtualBox", HypervisorPresent: true, "innotek GmbH", "VirtualBox VBOX   - 1", "0"),
        };
        probe.Keys.Add(@"SOFTWARE\Oracle\VirtualBox Guest Additions");
        probe.Services.Add("VBoxGuest");

        var indicator = Assert.Single(MachineDetector.Detect(probe));

        Assert.Equal(("VM_DETECTED", "VirtualBox"), (indicator.Type, indicator.Name));
        Assert.Equal("wmi, bios, registry, service:VBoxGuest", indicator.Detail);
    }

    [Fact]
    public void VMware_guest_is_detected()
    {
        var probe = new FakeProbe
        {
            Machine = new("VMware, Inc.", "VMware7,1", HypervisorPresent: true, "VMware, Inc.", "VMW71.00V", "VMware-56 4d 12 34"),
        };

        var indicator = Assert.Single(MachineDetector.Detect(probe));

        Assert.Equal(("VM_DETECTED", "VMware", "wmi, bios"), (indicator.Type, indicator.Name, indicator.Detail));
    }

    [Fact]
    public void Guest_drivers_alone_are_enough_when_wmi_is_disguised()
    {
        // WMI'daki üretici/model değiştirilebiliyor; konuk sürücüleri yine ele veriyor.
        var probe = new FakeProbe();
        probe.Services.Add("vmhgfs");

        Assert.Equal("service:vmhgfs", Assert.Single(MachineDetector.Detect(probe)).Detail);
    }

    [Fact]
    public void Having_VirtualBox_or_VMware_installed_on_a_physical_computer_is_not_a_vm()
    {
        // Ana bilgisayar kurulumunun anahtarları ve sürücüleri: aday sanal makinenin içinde değil.
        var probe = new FakeProbe();
        probe.Keys.UnionWith([@"SOFTWARE\Oracle\VirtualBox", @"SOFTWARE\VMware, Inc.\VMware Workstation"]);
        probe.Services.UnionWith(["VBoxSup", "VBoxNetLwf", "VBoxUSBMon", "vmx86", "hcmon", "VMAuthdService"]);

        Assert.Empty(MachineDetector.Detect(probe));
    }

    [Fact]
    public void Hyper_V_or_WSL2_on_a_physical_computer_gives_only_hypervisor_info()
    {
        // Kabul kriteri: Hyper-V/WSL2 açık fiziksel bilgisayarda yalnızca HYPERVISOR_INFO.
        var probe = new FakeProbe { Machine = new FakeProbe().Machine with { HypervisorPresent = true } };
        probe.Services.UnionWith(["vmcompute", "WslService", "LxssManager"]);

        var indicator = Assert.Single(MachineDetector.Detect(probe));

        Assert.Equal(("HYPERVISOR_INFO", "Hipervizör", "Hyper-V, WSL"), (indicator.Type, indicator.Name, indicator.Detail));
    }

    [Fact]
    public void Hypervisor_without_a_known_component_is_reported_as_vbs_or_similar()
    {
        var probe = new FakeProbe { Machine = new FakeProbe().Machine with { HypervisorPresent = true } };

        Assert.Equal("VBS ya da başka bir Windows bileşeni", Assert.Single(MachineDetector.Detect(probe)).Detail);
    }

    [Fact]
    public void Inside_a_vm_the_hypervisor_is_not_reported_separately()
    {
        var probe = new FakeProbe { Machine = new("innotek GmbH", "VirtualBox", HypervisorPresent: true) };
        probe.Services.Add("vmcompute");

        Assert.All(MachineDetector.Detect(probe), i => Assert.Equal("VM_DETECTED", i.Type));
    }

    [Fact]
    public void Unreadable_wmi_does_not_break_detection()
    {
        var probe = new FakeProbe { Machine = new MachineInfo() };
        probe.Services.Add("VBoxGuest");

        Assert.Equal("VirtualBox", Assert.Single(MachineDetector.Detect(probe)).Name);
    }
}

public class WindowsDeviceInspectorTests
{
    private static readonly ForbiddenAppList Apps = ForbiddenAppList.Load(Path.Combine(AppContext.BaseDirectory, "forbidden-apps.json"));

    private static (WindowsDeviceInspector Inspector, FakeProbe Probe, FakeTimeProvider Clock) Create(int scanSeconds = 15)
    {
        var probe = new FakeProbe();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 21, 9, 0, 0, TimeSpan.Zero));
        var options = Options.Create(new AgentOptions { ProcessScanIntervalSeconds = scanSeconds });
        return (new WindowsDeviceInspector(probe, Apps, clock, options), probe, clock);
    }

    [Theory]
    [InlineData("AnyDesk", "AnyDesk")]
    [InlineData("TeamViewer", "TeamViewer")]
    [InlineData("mstsc", "Uzak Masaüstü bağlantısı (mstsc)")]
    [InlineData("obs64", "OBS Studio (sanal kamera)")]
    [InlineData("Zoom", "Zoom")]
    [InlineData("Teams", "Microsoft Teams")]
    [InlineData("ms-teams", "Microsoft Teams")]
    [InlineData("Discord", "Discord")]
    [InlineData("openvpn", "OpenVPN")]
    [InlineData("openvpn-gui", "OpenVPN")]
    [InlineData("wireguard", "WireGuard")]
    public async Task Every_app_in_the_card_is_detected_with_the_shipped_list(string process, string name)
    {
        var (inspector, probe, _) = Create();
        probe.Processes.Add(process);

        var indicator = Assert.Single(await inspector.InspectAsync());

        Assert.Equal(name, indicator.Name);
        Assert.Equal(name is "OpenVPN" or "WireGuard" ? "VPN_PROXY" : "FORBIDDEN_APP", indicator.Type);
    }

    [Fact]
    public async Task Only_matches_are_returned_never_other_processes()
    {
        var (inspector, probe, _) = Create();
        probe.Processes = ["explorer", "chrome", "WINWORD", "Spotify", "zoom"];

        var found = await inspector.InspectAsync();

        Assert.Equal(["Zoom"], found.Select(i => i.Name));
    }

    [Fact]
    public async Task Incoming_remote_desktop_is_detected_even_without_mstsc()
    {
        // Kart: mstsc yalnızca adayın başka bilgisayara bağlandığını gösterir; dışarıdan bağlanılması ayrı denetleniyor.
        var (inspector, probe, _) = Create();
        probe.RemoteSession = true;

        var indicator = Assert.Single(await inspector.InspectAsync());

        Assert.Equal(WindowsDeviceInspector.IncomingRemoteDesktop, indicator);
        Assert.Equal("FORBIDDEN_APP", indicator.Type);
    }

    [Fact]
    public async Task Machine_is_inspected_once_and_processes_only_every_scan_interval()
    {
        var (inspector, probe, clock) = Create(scanSeconds: 15);

        for (var i = 0; i < 6; i++) // 5 sn aralıkla 6 sinyal = 25 sn
        {
            await inspector.InspectAsync();
            clock.Advance(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(1, probe.MachineReads);
        Assert.Equal(2, probe.ProcessScans); // 0. ve 15. saniye
    }

    [Fact]
    public async Task Between_scans_the_last_result_is_kept_and_remote_session_is_checked_every_time()
    {
        var (inspector, probe, clock) = Create(scanSeconds: 15);
        probe.Processes.Add("Discord");
        await inspector.InspectAsync();

        probe.Processes.Remove("Discord");
        probe.RemoteSession = true;
        clock.Advance(TimeSpan.FromSeconds(5));
        var between = await inspector.InspectAsync();

        clock.Advance(TimeSpan.FromSeconds(10));
        var after = await inspector.InspectAsync();

        Assert.Equal(["Discord", "Uzak Masaüstü oturumu"], between.Select(i => i.Name));
        Assert.Equal(["Uzak Masaüstü oturumu"], after.Select(i => i.Name));
    }

    [Fact]
    public async Task Vm_and_forbidden_app_are_reported_together()
    {
        var (inspector, probe, _) = Create();
        probe.Machine = new("VMware, Inc.", "VMware7,1", HypervisorPresent: true);
        probe.Processes.Add("AnyDesk");

        var found = await inspector.InspectAsync();

        Assert.Equal(["VM_DETECTED", "FORBIDDEN_APP"], found.Select(i => i.Type));
    }
}

public class NetworkDetectorTests
{
    [Fact]
    public void Ordinary_computer_with_hyper_v_wsl_virtualbox_and_vmware_host_adapters_has_no_vpn()
    {
        // Bu bilgisayardaki gerçek bağdaştırıcılar: hiçbiri VPN değil.
        var probe = new FakeProbe
        {
            Adapters =
            [
                new("vEthernet (WSL (Hyper-V firewall))", "Hyper-V Virtual Ethernet Adapter", "Ethernet"),
                new("Ethernet 2", "VirtualBox Host-Only Ethernet Adapter", "Ethernet"),
                new("VMware Network Adapter VMnet1", "VMware Virtual Ethernet Adapter for VMnet1", "Ethernet"),
                new("VMware Network Adapter VMnet8", "VMware Virtual Ethernet Adapter for VMnet8", "Ethernet"),
                new("Wi-Fi", "MediaTek MT7921 Wi-Fi 6 802.11ax PCIe Adapter", "Wireless80211"),
                new("Bluetooth Ağ Bağlantısı 3", "Bluetooth Device (Personal Area Network) #3", "Ethernet"),
                new("Teredo", "Teredo Tunneling Pseudo-Interface", "Tunnel"),
            ],
        };

        Assert.Empty(NetworkDetector.Detect(probe));
    }

    [Theory]
    [InlineData("OpenVPN TAP-Windows6", "TAP-Windows Adapter V9", "Ethernet")]
    [InlineData("OpenVPN Wintun", "Wintun Userspace Tunnel", "Unknown")]
    [InlineData("wg-ev", "WireGuard Tunnel", "Unknown")]
    [InlineData("NordLynx", "NordLynx Tunnel", "Unknown")]
    [InlineData("CloudflareWARP", "Cloudflare WARP Interface Tunnel", "Unknown")]
    [InlineData("Okul VPN", "WAN Miniport (IKEv2)", "Ppp")]
    [InlineData("Ethernet 3", "Cisco AnyConnect Secure Mobility Client Virtual Miniport Adapter for Windows x64", "Ethernet")]
    public void Active_vpn_tun_tap_adapters_are_detected(string name, string description, string kind)
    {
        var probe = new FakeProbe();
        probe.Adapters.Add(new NetworkAdapter(name, description, kind));

        var indicator = Assert.Single(NetworkDetector.Detect(probe));

        Assert.Equal(("VPN_PROXY", "VPN bağdaştırıcısı"), (indicator.Type, indicator.Name));
        Assert.Contains(description, indicator.Detail);
    }

    [Fact]
    public void Several_vpn_adapters_are_one_indicator()
    {
        var probe = new FakeProbe();
        probe.Adapters.Add(new("a", "TAP-Windows Adapter V9", "Ethernet"));
        probe.Adapters.Add(new("b", "WireGuard Tunnel", "Unknown"));

        Assert.Single(NetworkDetector.Detect(probe));
    }

    [Fact]
    public void System_proxy_settings_are_detected_but_automatic_detection_is_not()
    {
        var none = new FakeProbe { Proxy = new ProxySettings() }; // "Ayarları otomatik algıla" açık olsa da gösterge değil
        var all = new FakeProbe { Proxy = new ProxySettings("127.0.0.1:8080", "http://wpad.ornek/proxy.pac", "vekil.ornek:3128") };

        Assert.Empty(NetworkDetector.Detect(none));
        Assert.Equal(
            ["Vekil sunucu (Windows ayarı)", "Vekil sunucu (otomatik yapılandırma betiği)", "Vekil sunucu (WinHTTP)"],
            NetworkDetector.Detect(all).Select(i => i.Name));
        Assert.All(NetworkDetector.Detect(all), i => Assert.Equal("VPN_PROXY", i.Type));
    }

    [Fact]
    public async Task Network_is_checked_with_the_process_scan_not_every_signal()
    {
        var probe = new FakeProbe();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero));
        var apps = ForbiddenAppList.Load(Path.Combine(AppContext.BaseDirectory, "forbidden-apps.json"));
        var inspector = new WindowsDeviceInspector(probe, apps, clock, Options.Create(new AgentOptions { ProcessScanIntervalSeconds = 15 }));

        await inspector.InspectAsync();
        probe.Adapters.Add(new("wg", "WireGuard Tunnel", "Unknown"));
        clock.Advance(TimeSpan.FromSeconds(5));
        var before = await inspector.InspectAsync();
        clock.Advance(TimeSpan.FromSeconds(10));
        var after = await inspector.InspectAsync();

        Assert.Empty(before);
        Assert.Equal("VPN_PROXY", Assert.Single(after).Type);
    }
}

/// <summary>Gerçek Windows kaynaklarıyla: yönetici izni olmadan çalışmalı ve hata fırlatmamalı.</summary>
public class WindowsSystemProbeTests
{
    private readonly WindowsSystemProbe _probe = new(NullLogger<WindowsSystemProbe>.Instance);

    [Fact]
    public void Real_probe_reads_without_admin_rights()
    {
        var machine = _probe.ReadMachineInfo();
        var processes = _probe.RunningProcessNames();

        Assert.NotNull(machine.Manufacturer);
        Assert.NotNull(machine.HypervisorPresent);
        Assert.Contains(processes, p => p.Equals("dotnet", StringComparison.OrdinalIgnoreCase)
                                        || p.StartsWith("testhost", StringComparison.OrdinalIgnoreCase));
        Assert.True(_probe.ServiceExists("EventLog"));
        Assert.DoesNotContain(_probe.ActiveNetworkAdapters(), a => a.Kind == "Loopback");
        _ = _probe.ReadProxySettings(); // winhttp.dll çağrıları yönetici izni olmadan hata vermemeli
        Assert.False(_probe.ServiceExists("aiguard-olmayan-servis"));
        _ = _probe.IsRemoteSession();
    }

    [Fact]
    public void Real_inspection_only_produces_known_indicator_types()
    {
        // CI'daki Windows makinesi bir Azure sanal makinesi (Hyper-V): HYPERVISOR_INFO beklenebilir,
        // VirtualBox/VMware beklenmez. Burada yalnızca türlerin ortak adlardan olduğu sınanıyor.
        var found = MachineDetector.Detect(_probe);

        Assert.All(found, i => Assert.Contains(i.Type, new[] { "VM_DETECTED", "HYPERVISOR_INFO" }));
    }
}
