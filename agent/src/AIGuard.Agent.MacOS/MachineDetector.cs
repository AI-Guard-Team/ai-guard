using AIGuard.Agent.Core.Platform;

namespace AIGuard.Agent.MacOS;

/// <summary>
/// Parallels, VMware Fusion ve Apple sanal makinesi konuğu (Y5, FR-18). Sınav boyunca değişmediği için
/// <see cref="MacDeviceInspector"/> bunu bir kez hesaplıyor.
/// </summary>
/// <remarks>
/// <para>
/// Ana bilgisayarda Parallels Desktop ya da VMware Fusion'ın <b>kurulu olması</b> sanal makine
/// içinde olunduğu anlamına gelmiyor. Bu yüzden uygulama listesi ve bütün <c>ioreg</c> ağacı
/// okunmuyor. Yalnızca bu makineyi anlatan alanlara bakılıyor: <c>system_profiler</c> model adı
/// ve <c>IOPlatformExpertDevice</c> (üretici, ürün, model).
/// </para>
/// <para>
/// <c>kern.hv_vmm_present</c> ürün adı vermez (UTM ve Apple sanal makinesi de 1 döner). Ürün
/// belli ise <c>VM_DETECTED</c> ve ad Windows ile aynı yazılır: <c>Parallels</c>, <c>VMware</c>.
/// </para>
/// <para>
/// Apple Silicon'da Parallels, UTM ve VMware macOS konuğunu Apple'ın Virtualization çerçevesiyle
/// çalıştırıyor; konuk kendini ürün adıyla değil <c>VirtualMac</c> modeliyle ("Apple Virtual
/// Machine") tanıtıyor. Fiziksel bir Mac bu modeli vermediği için bu kesin bir sanal makine
/// göstergesi; ürün ayırt edilemese de <c>VM_DETECTED</c> ve <c>Apple Sanal Makine</c> adı
/// gönderiliyor. Hiçbir ürün izi yokken yalnızca <c>kern.hv_vmm_present</c> 1 ise
/// <c>HYPERVISOR_INFO</c> kalıyor.
/// </para>
/// </remarks>
internal static class MachineDetector
{
    /// <summary>Apple Silicon konuğunda ürün (Parallels, UTM, VMware) ayırt edilemediğinde gönderilen ad.</summary>
    internal const string AppleVirtualMachineName = "Apple Sanal Makine";

    public static IReadOnlyList<DeviceIndicator> Detect(IMacSystemProbe probe)
    {
        var hardware = probe.ReadHardware();

        var vms = new List<DeviceIndicator>();
        AddGuest(vms, hardware, "Parallels", ["Parallels"], ["Parallels"]);
        AddGuest(vms, hardware, "VMware", ["VMware"], ["VMware"]);

        // Ürün adı yoksa: Apple'ın kendi sanal makine modeli (Apple Silicon konukları).
        if (vms.Count == 0)
        {
            AddGuest(vms, hardware, AppleVirtualMachineName, ["Apple Virtual Machine", "VirtualMac"], ["VirtualMac"]);
        }

        if (vms.Count > 0)
        {
            return vms;
        }

        if (hardware.HypervisorPresent == true)
        {
            return [new DeviceIndicator(DeviceIndicator.HypervisorInfo, "Hipervizör", "sysctl:kern.hv_vmm_present")];
        }

        return [];
    }

    private static void AddGuest(
        List<DeviceIndicator> vms, MacHardware hardware, string product, string[] profilerTokens, string[] registryTokens)
    {
        var evidence = Evidence(hardware, profilerTokens, registryTokens);
        if (evidence.Count == 0)
        {
            return;
        }

        vms.Add(new DeviceIndicator(DeviceIndicator.VmDetected, product, DeviceIndicator.Truncate(string.Join(", ", evidence))));
    }

    /// <summary>Değerlendirici için nereden anlaşıldığı. Ürün izi yoksa sysctl tek başına yetmez.</summary>
    private static List<string> Evidence(MacHardware hardware, string[] profilerTokens, string[] registryTokens)
    {
        var profiler = Has(profilerTokens, hardware.ModelName, hardware.ModelIdentifier);
        var registry = Has(registryTokens, hardware.IoManufacturer, hardware.IoProduct, hardware.IoModel);
        if (!profiler && !registry)
        {
            return [];
        }

        var evidence = new List<string>();
        if (hardware.HypervisorPresent == true)
        {
            evidence.Add("sysctl:kern.hv_vmm_present");
        }

        if (profiler)
        {
            evidence.Add("system_profiler:model");
        }

        if (registry)
        {
            evidence.Add("ioreg:model");
        }

        return evidence;
    }

    private static bool Has(string[] parts, params string?[] values) =>
        values.Any(value => value is not null
            && parts.Any(part => value.Contains(part, StringComparison.OrdinalIgnoreCase)));
}
