using AIGuard.Agent.MacOS;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIGuard.Agent.MacOS.Tests;

internal sealed class FakeProbe : IMacSystemProbe
{
    public MacHardware Hardware { get; set; } = new(
        HypervisorPresent: false,
        ModelName: "MacBook Air",
        ModelIdentifier: "Mac14,2",
        IoManufacturer: "Apple Inc.",
        IoModel: "Mac14,2");

    public int Reads { get; private set; }
    public int ProcessScans { get; private set; }

    /// <summary>Çalışan süreçler. Varsayılan: sıradan bir Mac masaüstü.</summary>
    public List<string> Processes { get; set; } = ["Finder", "Dock", "Safari", "Spotify", "Cursor"];

    /// <summary>Hedef → çıkış arayüzü. Varsayılan: Wi-Fi (VPN yok) ve macOS'un boşta duran utun arayüzleri.</summary>
    public Dictionary<string, string?> Routes { get; set; } = new()
    {
        ["default"] = "en0",
        [NetworkDetector.PublicProbeAddress] = "en0",
    };

    public MacProxySettings Proxy { get; set; } = new();

    public MacHardware ReadHardware()
    {
        Reads++;
        return Hardware;
    }

    public IReadOnlyList<string> RunningProcessNames()
    {
        ProcessScans++;
        return Processes;
    }

    public string? RouteInterface(string destination) => Routes.GetValueOrDefault(destination);

    public MacProxySettings ReadProxySettings() => Proxy;
}

public class MachineDetectorTests
{
    [Fact]
    public void Ordinary_physical_mac_has_no_indicator()
    {
        Assert.Empty(MachineDetector.Detect(new FakeProbe()));
    }

    [Fact]
    public void Parallels_guest_is_detected_from_profiler_ioreg_and_sysctl()
    {
        var probe = new FakeProbe
        {
            Hardware = new(
                HypervisorPresent: true,
                ModelName: "Parallels ARM Virtual Machine",
                ModelIdentifier: "Parallels-1.0",
                IoManufacturer: "Parallels International GmbH",
                IoProduct: "Parallels Desktop",
                IoModel: "Parallels-1.0"),
        };

        var indicator = Assert.Single(MachineDetector.Detect(probe));

        Assert.Equal(("VM_DETECTED", "Parallels"), (indicator.Type, indicator.Name));
        Assert.Equal("sysctl:kern.hv_vmm_present, system_profiler:model, ioreg:model", indicator.Detail);
    }

    [Fact]
    public void VMware_guest_is_detected_from_the_model_even_without_apple_hypervisor_flag()
    {
        // Intel Fusion kendi hipervizörünü kullanıyor; kern.hv_vmm_present 0 kalabiliyor.
        var probe = new FakeProbe
        {
            Hardware = new(HypervisorPresent: false, ModelName: "VMware Virtual Platform", ModelIdentifier: "VMware20,1"),
        };

        var indicator = Assert.Single(MachineDetector.Detect(probe));

        Assert.Equal(("VM_DETECTED", "VMware", "system_profiler:model"), (indicator.Type, indicator.Name, indicator.Detail));
    }

    [Fact]
    public void Having_Parallels_or_VMware_installed_on_a_physical_mac_is_not_a_vm()
    {
        // Kurulum izi okunmuyor. Bu makinenin modeli gerçek Mac ise gösterge yok.
        var probe = new FakeProbe
        {
            Hardware = new(
                HypervisorPresent: false,
                ModelName: "MacBook Air",
                ModelIdentifier: "Mac14,2",
                IoManufacturer: "Apple Inc.",
                IoModel: "Mac14,2"),
        };

        Assert.Empty(MachineDetector.Detect(probe));
    }

    [Fact]
    public void Apple_silicon_guest_without_a_product_name_is_a_vm()
    {
        // Parallels, UTM ve VMware Apple Silicon'da macOS konuğunu Apple'ın Virtualization çerçevesiyle çalıştırıyor;
        // konuk "Apple Virtual Machine" / VirtualMac2,1 diyor, ürün adı yok.
        var probe = new FakeProbe
        {
            Hardware = new(
                HypervisorPresent: true,
                ModelName: "Apple Virtual Machine 1",
                ModelIdentifier: "VirtualMac2,1",
                IoManufacturer: "Apple Inc.",
                IoModel: "VirtualMac2,1"),
        };

        var indicator = Assert.Single(MachineDetector.Detect(probe));

        Assert.Equal(("VM_DETECTED", "Apple Sanal Makine"), (indicator.Type, indicator.Name));
        Assert.Equal("sysctl:kern.hv_vmm_present, system_profiler:model, ioreg:model", indicator.Detail);
    }

    [Fact]
    public void Apple_silicon_guest_is_a_vm_even_when_sysctl_is_unreadable()
    {
        // Gerçek Parallels macOS konuğunda sysctl okunamadı; model alanları yetiyor.
        var probe = new FakeProbe
        {
            Hardware = new(ModelName: "Apple Virtual Machine 1", ModelIdentifier: "VirtualMac2,1"),
        };

        var indicator = Assert.Single(MachineDetector.Detect(probe));

        Assert.Equal(("VM_DETECTED", "Apple Sanal Makine", "system_profiler:model"), (indicator.Type, indicator.Name, indicator.Detail));
    }

    [Fact]
    public void Named_product_wins_over_the_generic_apple_vm_name()
    {
        var probe = new FakeProbe
        {
            Hardware = new(
                HypervisorPresent: true,
                ModelName: "Parallels ARM Virtual Machine",
                ModelIdentifier: "VirtualMac2,1",
                IoModel: "VirtualMac2,1"),
        };

        var indicator = Assert.Single(MachineDetector.Detect(probe));

        Assert.Equal("Parallels", indicator.Name);
    }

    [Fact]
    public void Hypervisor_flag_alone_without_any_vm_model_is_only_hypervisor_info()
    {
        // Fiziksel Mac modeli, ama çekirdek hipervizör bildiriyor: sanal makine değil, yalnızca bilgi.
        var probe = new FakeProbe
        {
            Hardware = new(HypervisorPresent: true, ModelName: "MacBook Air", ModelIdentifier: "Mac14,2", IoModel: "Mac14,2"),
        };

        var indicator = Assert.Single(MachineDetector.Detect(probe));

        Assert.Equal(("HYPERVISOR_INFO", "Hipervizör", "sysctl:kern.hv_vmm_present"), (indicator.Type, indicator.Name, indicator.Detail));
    }

    [Fact]
    public void Inside_a_vm_the_hypervisor_is_not_reported_separately()
    {
        var probe = new FakeProbe
        {
            Hardware = new(HypervisorPresent: true, IoModel: "VMware20,1"),
        };

        Assert.All(MachineDetector.Detect(probe), indicator => Assert.Equal("VM_DETECTED", indicator.Type));
    }

    [Fact]
    public void Unreadable_hardware_does_not_break_detection()
    {
        Assert.Empty(MachineDetector.Detect(new FakeProbe { Hardware = new MacHardware() }));
    }
}

public class MacDeviceInspectorTests
{
    [Fact]
    public async Task Hardware_is_read_once()
    {
        var probe = new FakeProbe();
        var inspector = MacTestHost.CreateInspector(probe);

        await inspector.InspectAsync();
        await inspector.InspectAsync();

        Assert.Equal(1, probe.Reads);
        Assert.Equal("MacOS", inspector.Platform);
    }
}

public class MacHardwareTextTests
{
    private const string Profiler = """
        Hardware:
            Model Name: MacBook Air
            Model Identifier: Mac14,2
            Serial Number (system): GİZLİ
        """;

    private const string Registry = """
          "manufacturer" = <"Apple Inc.">
          "model" = <"Mac14,2">
        """;

    [Fact]
    public void Profiler_fields_and_ioreg_strings_parse()
    {
        Assert.Equal("MacBook Air", MacSystemProbe.Field(Profiler, "Model Name"));
        Assert.Equal("Mac14,2", MacSystemProbe.Field(Profiler, "Model Identifier"));
        Assert.Equal("Apple Inc.", MacSystemProbe.IoregValue(Registry, "manufacturer"));
        Assert.Equal("Mac14,2", MacSystemProbe.IoregValue(Registry, "model"));
        Assert.Null(MacSystemProbe.IoregValue(Registry, "product-name"));
    }

    [Fact]
    public void Live_read_on_macos_returns_a_model()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var hardware = new MacSystemProbe(NullLogger<MacSystemProbe>.Instance).ReadHardware();

        Assert.False(string.IsNullOrWhiteSpace(hardware.ModelName));
        Assert.False(string.IsNullOrWhiteSpace(hardware.ModelIdentifier));
    }
}
