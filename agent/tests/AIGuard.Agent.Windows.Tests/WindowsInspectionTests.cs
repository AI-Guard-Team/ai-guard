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
    public async Task Every_app_in_the_card_is_detected_with_the_shipped_list(string process, string name)
    {
        var (inspector, probe, _) = Create();
        probe.Processes.Add(process);

        var indicator = Assert.Single(await inspector.InspectAsync());

        Assert.Equal(("FORBIDDEN_APP", name), (indicator.Type, indicator.Name));
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
