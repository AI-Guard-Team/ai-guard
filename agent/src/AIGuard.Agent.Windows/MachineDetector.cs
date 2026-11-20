using AIGuard.Agent.Core.Platform;

namespace AIGuard.Agent.Windows;

/// <summary>
/// Sanal makine (FR-18) ve genel hipervizör göstergeleri. Bir sınav boyunca değişmedikleri için
/// <see cref="WindowsDeviceInspector"/> bunları bir kez hesaplıyor.
/// </summary>
/// <remarks>
/// <para>
/// VirtualBox ve VMware için yalnızca <b>konuk</b> izleri aranıyor: bilgisayarda VirtualBox ya da
/// VMware Workstation <b>kurulu olması</b> sanal makine içinde olunduğu anlamına gelmiyor. Bu yüzden
/// ana bilgisayar kurulumunun anahtarları (ör. <c>SOFTWARE\Oracle\VirtualBox</c>, <c>VBoxSup</c>,
/// <c>vmx86</c>) aranmıyor; konuk eklentileri ve konuk sürücüleri aranıyor.
/// </para>
/// <para>
/// Hyper-V, WSL2, VBS ve Docker Windows'un kendi hipervizörünü kullanıyor ve fiziksel bilgisayarlarda
/// yaygın. Bunlar <c>HYPERVISOR_INFO</c> olarak, düşük ağırlıklı bilgi amacıyla bildiriliyor; tek
/// başına oturumu göstergeli yapmıyor (UC-08 4b).
/// </para>
/// </remarks>
internal static class MachineDetector
{
    private sealed record VmSignature(
        string Name,
        Func<MachineInfo, bool> Wmi,
        Func<MachineInfo, bool> Bios,
        string[] RegistryKeys,
        string[] Services);

    private static readonly VmSignature[] Signatures =
    [
        new("VirtualBox",
            Wmi: m => Has(m.Model, "VirtualBox") || Has(m.Manufacturer, "innotek"),
            Bios: m => Has(m.BiosVersion, "VBOX") || Has(m.BiosVersion, "VirtualBox") || Has(m.BiosManufacturer, "innotek"),
            RegistryKeys: [@"SOFTWARE\Oracle\VirtualBox Guest Additions"],
            Services: ["VBoxGuest", "VBoxMouse", "VBoxSF", "VBoxService", "VBoxVideo"]),
        new("VMware",
            Wmi: m => Has(m.Manufacturer, "VMware") || Has(m.Model, "VMware"),
            Bios: m => Has(m.BiosManufacturer, "VMware") || (m.BiosSerial?.StartsWith("VMware-", StringComparison.OrdinalIgnoreCase) ?? false),
            RegistryKeys: [@"SOFTWARE\VMware, Inc.\VMware Tools"],
            Services: ["VMTools", "vmhgfs", "vmmouse", "vm3dmp"]),
    ];

    /// <summary>Windows hipervizörünü kullanan bileşenlerin servisleri; yalnızca ayrıntıyı zenginleştiriyor.</summary>
    private static readonly (string Service, string Label)[] HypervisorHints =
    [
        ("vmcompute", "Hyper-V"),
        ("WslService", "WSL"),
        ("LxssManager", "WSL"),
        ("com.docker.service", "Docker Desktop"),
    ];

    public static IReadOnlyList<DeviceIndicator> Detect(IWindowsSystemProbe probe)
    {
        var machine = probe.ReadMachineInfo();

        var vms = Signatures
            .Select(sig => (sig.Name, evidence: Evidence(sig, machine, probe)))
            .Where(x => x.evidence.Count > 0)
            .Select(x => new DeviceIndicator(DeviceIndicator.VmDetected, x.Name, DeviceIndicator.Truncate(string.Join(", ", x.evidence))))
            .ToList();

        // Sanal makinenin içinde hipervizör zaten var; ayrıca bilgi olayı göndermek tekrar olur.
        if (vms.Count > 0)
        {
            return vms;
        }

        if (machine.HypervisorPresent == true)
        {
            var hints = HypervisorHints.Where(h => probe.ServiceExists(h.Service)).Select(h => h.Label).Distinct().ToList();
            if (Has(machine.Manufacturer, "Microsoft") && Has(machine.Model, "Virtual Machine"))
            {
                hints.Insert(0, "Hyper-V sanal makinesi");
            }

            var detail = hints.Count > 0 ? string.Join(", ", hints) : "VBS ya da başka bir Windows bileşeni";
            return [new DeviceIndicator(DeviceIndicator.HypervisorInfo, "Hipervizör", DeviceIndicator.Truncate(detail))];
        }

        return [];
    }

    /// <summary>Değerlendirici için nereden anlaşıldığı (ör. "wmi, bios, service:VBoxGuest").</summary>
    private static List<string> Evidence(VmSignature sig, MachineInfo machine, IWindowsSystemProbe probe)
    {
        var evidence = new List<string>();
        if (sig.Wmi(machine))
        {
            evidence.Add("wmi");
        }

        if (sig.Bios(machine))
        {
            evidence.Add("bios");
        }

        if (sig.RegistryKeys.Any(probe.LocalMachineKeyExists))
        {
            evidence.Add("registry");
        }

        evidence.AddRange(sig.Services.Where(probe.ServiceExists).Select(s => "service:" + s));
        return evidence;
    }

    private static bool Has(string? value, string part) =>
        value?.Contains(part, StringComparison.OrdinalIgnoreCase) ?? false;
}
