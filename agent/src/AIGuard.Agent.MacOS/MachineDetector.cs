using AIGuard.Agent.Core.Platform;

namespace AIGuard.Agent.MacOS;

/// <summary>
/// Parallels ve VMware Fusion konuğu (Y5, FR-18). Sınav boyunca değişmediği için
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
/// belli değilse <c>HYPERVISOR_INFO</c>; belli ise <c>VM_DETECTED</c> ve ad Windows ile aynı
/// yazılır: <c>Parallels</c>, <c>VMware</c>.
/// </para>
/// </remarks>
internal static class MachineDetector
{
    public static IReadOnlyList<DeviceIndicator> Detect(IMacSystemProbe probe)
    {
        var hardware = probe.ReadHardware();

        var vms = new List<DeviceIndicator>();
        AddGuest(vms, hardware, "Parallels");
        AddGuest(vms, hardware, "VMware");
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

    private static void AddGuest(List<DeviceIndicator> vms, MacHardware hardware, string product)
    {
        var evidence = Evidence(hardware, product);
        if (evidence.Count == 0)
        {
            return;
        }

        vms.Add(new DeviceIndicator(DeviceIndicator.VmDetected, product, DeviceIndicator.Truncate(string.Join(", ", evidence))));
    }

    /// <summary>Değerlendirici için nereden anlaşıldığı. Ürün izi yoksa sysctl tek başına yetmez.</summary>
    private static List<string> Evidence(MacHardware hardware, string product)
    {
        var profiler = Has(product, hardware.ModelName, hardware.ModelIdentifier);
        var registry = Has(product, hardware.IoManufacturer, hardware.IoProduct, hardware.IoModel);
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

    private static bool Has(string part, params string?[] values) =>
        values.Any(value => value?.Contains(part, StringComparison.OrdinalIgnoreCase) ?? false);
}
