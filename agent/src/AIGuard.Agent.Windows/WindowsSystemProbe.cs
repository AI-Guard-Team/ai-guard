using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AIGuard.Agent.Windows;

/// <summary>Gerçek Windows kaynakları: WMI, kayıt defteri, süreç listesi, user32, winhttp.</summary>
/// <remarks>
/// Bu sınıf yalnızca veriyi okuyor; "bu bir sanal makine mi, bu bir VPN mi" kararı <see cref="MachineDetector"/>,
/// <see cref="NetworkDetector"/> ve <see cref="WindowsDeviceInspector"/>'da. Böylece karar kuralları
/// <see cref="IWindowsSystemProbe"/>'un sahtesiyle, gerçek Windows olmadan test edilebiliyor (WindowsInspectionTests).
/// Hiçbir çağrı yönetici izni istemiyor; okunamayan kaynak hata fırlatmak yerine "yok" sayılıyor ki ajan çökmesin.
/// </remarks>
internal sealed class WindowsSystemProbe(ILogger<WindowsSystemProbe> logger) : IWindowsSystemProbe
{
    // GetSystemMetrics(SM_REMOTESESSION) uzak masaüstü (RDP) oturumunda sıfırdan farklı dönüyor.
    private const int SM_REMOTESESSION = 0x1000;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    /// <summary>
    /// Üretici, model ve BIOS bilgisi (WMI: Win32_ComputerSystem, Win32_BIOS). Sanal makinelerde bu alanlarda
    /// "VMware", "VirtualBox", "innotek" gibi izler oluyor. HypervisorPresent Windows'un kendi hipervizörünü de gösteriyor.
    /// </summary>
    public MachineInfo ReadMachineInfo()
    {
        var info = new MachineInfo();
        try
        {
            using (var cs = new ManagementObjectSearcher("SELECT Manufacturer, Model, HypervisorPresent FROM Win32_ComputerSystem"))
            {
                foreach (var o in cs.Get())
                {
                    using (o)
                    {
                        info = info with
                        {
                            Manufacturer = o["Manufacturer"] as string,
                            Model = o["Model"] as string,
                            HypervisorPresent = o["HypervisorPresent"] as bool?,
                        };
                    }
                }
            }

            using var bios = new ManagementObjectSearcher("SELECT Manufacturer, SMBIOSBIOSVersion, Version, SerialNumber FROM Win32_BIOS");
            foreach (var o in bios.Get())
            {
                using (o)
                {
                    info = info with
                    {
                        BiosManufacturer = o["Manufacturer"] as string,
                        BiosVersion = string.Join(" ", new[] { o["SMBIOSBIOSVersion"] as string, o["Version"] as string }
                            .Where(v => !string.IsNullOrWhiteSpace(v))),
                        BiosSerial = o["SerialNumber"] as string,
                    };
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
            // WMI kapalı ya da bozuk olabilir; diğer denetimler (kayıt defteri, servisler) yine çalışıyor.
            logger.LogWarning("WMI okunamadı ({Error}); sanal makine denetimi kayıt defteri ve servislerle sürüyor.", ex.Message);
        }

        return info;
    }

    /// <summary>HKLM altında anahtar var mı (64 bit görünüm); konuk eklentilerinin kurulum izleri burada.</summary>
    public bool LocalMachineKeyExists(string path)
    {
        try
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = hklm.OpenSubKey(path);
            return key is not null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>Servis kayıtlı mı; servis listesini sorgulamak yerine kayıt defterindeki servis anahtarına bakılıyor.</summary>
    public bool ServiceExists(string name) => LocalMachineKeyExists(@"SYSTEM\CurrentControlSet\Services\" + name);

    /// <summary>
    /// Çalışan süreçlerin yalnızca adları. Liste ajandan dışarı çıkmıyor; yasaklı listeyle eşleşenler gösterge oluyor (KVKK).
    /// </summary>
    public IReadOnlyList<string> RunningProcessNames()
    {
        var processes = Process.GetProcesses();
        var names = new List<string>(processes.Length);
        foreach (var p in processes)
        {
            try
            {
                names.Add(p.ProcessName);
            }
            catch (InvalidOperationException)
            {
                // Süreç bu arada kapandı.
            }
            finally
            {
                p.Dispose();
            }
        }

        return names;
    }

    /// <summary>Ajan bir uzak masaüstü oturumunda mı çalışıyor (Y3, FORBIDDEN_APP "Uzak masaüstü").</summary>
    public bool IsRemoteSession() => GetSystemMetrics(SM_REMOTESESSION) != 0;

    /// <summary>Açık ağ bağdaştırıcıları (Y4): VPN/tünel bağdaştırıcısı adı ve açıklamasından anlaşılıyor.</summary>
    public IReadOnlyList<NetworkAdapter> ActiveNetworkAdapters()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(n => new NetworkAdapter(n.Name, n.Description, n.NetworkInterfaceType.ToString()))
                .ToList();
        }
        catch (NetworkInformationException ex)
        {
            logger.LogWarning("Ağ bağdaştırıcıları okunamadı ({Error}).", ex.Message);
            return [];
        }
    }

    /// <summary>
    /// Vekil sunucu ayarı (Y4): kullanıcının tarayıcı/Windows ayarı (elle vekil ya da PAC adresi) ve makine geneli WinHTTP ayarı.
    /// </summary>
    public ProxySettings ReadProxySettings()
    {
        string? userProxy = null, pac = null, machine = null;

        var ie = default(WinHttpCurrentUserIeProxyConfig);
        if (WinHttpGetIEProxyConfigForCurrentUser(ref ie))
        {
            userProxy = TakeString(ie.Proxy);
            pac = TakeString(ie.AutoConfigUrl);
            _ = TakeString(ie.ProxyBypass);
        }

        var info = default(WinHttpProxyInfo);
        if (WinHttpGetDefaultProxyConfiguration(ref info))
        {
            var proxy = TakeString(info.Proxy);
            _ = TakeString(info.ProxyBypass);
            machine = info.AccessType == WINHTTP_ACCESS_TYPE_NAMED_PROXY ? proxy : null;
        }

        return new ProxySettings(userProxy, pac, machine);
    }

    // winhttp.dll: yönetici izni gerektirmiyor. Dönen metinler GlobalFree ile serbest bırakılmalı.
    private const uint WINHTTP_ACCESS_TYPE_NAMED_PROXY = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct WinHttpCurrentUserIeProxyConfig
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool AutoDetect;
        public IntPtr AutoConfigUrl;
        public IntPtr Proxy;
        public IntPtr ProxyBypass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinHttpProxyInfo
    {
        public uint AccessType;
        public IntPtr Proxy;
        public IntPtr ProxyBypass;
    }

    [DllImport("winhttp.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WinHttpGetIEProxyConfigForCurrentUser(ref WinHttpCurrentUserIeProxyConfig config);

    [DllImport("winhttp.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WinHttpGetDefaultProxyConfiguration(ref WinHttpProxyInfo info);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr handle);

    /// <summary>winhttp'nin ayırdığı metni okuyup belleği serbest bırakır; boş metin null.</summary>
    private static string? TakeString(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var value = Marshal.PtrToStringUni(ptr);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        finally
        {
            GlobalFree(ptr);
        }
    }
}
