namespace AIGuard.Agent.Windows;

/// <summary>WMI'dan okunan donanım bilgisi; okunamayan alan null.</summary>
internal sealed record MachineInfo(
    string? Manufacturer = null,
    string? Model = null,
    bool? HypervisorPresent = null,
    string? BiosManufacturer = null,
    string? BiosVersion = null,
    string? BiosSerial = null);

/// <summary>
/// İşletim sisteminden okunan ham bilgiler. Kurallar (<see cref="MachineDetector"/>,
/// <see cref="WindowsDeviceInspector"/>) bu arayüz üzerinden çalışıyor; testlerde sahte uygulama
/// verilerek VirtualBox, VMware ya da uzak masaüstü olmayan bir bilgisayarda da sınanabiliyor.
/// </summary>
/// <remarks>Bütün çağrılar kullanıcı modunda çalışıyor; yönetici izni istemiyor. Hata fırlatmıyor.</remarks>
internal interface IWindowsSystemProbe
{
    /// <summary>Win32_ComputerSystem ve Win32_BIOS.</summary>
    MachineInfo ReadMachineInfo();

    /// <summary>HKEY_LOCAL_MACHINE altında anahtar var mı (64 bit görünüm).</summary>
    bool LocalMachineKeyExists(string path);

    /// <summary>Sürücü ya da servis kayıtlı mı (HKLM\SYSTEM\CurrentControlSet\Services\{ad}).</summary>
    bool ServiceExists(string name);

    /// <summary>Çalışan süreçlerin adları. Yalnızca yasaklı yazılım listesiyle karşılaştırmak için; hiçbir yere gönderilmiyor.</summary>
    IReadOnlyList<string> RunningProcessNames();

    /// <summary>Bu oturum Uzak Masaüstü üzerinden mi açılmış (GetSystemMetrics(SM_REMOTESESSION)).</summary>
    bool IsRemoteSession();
}
