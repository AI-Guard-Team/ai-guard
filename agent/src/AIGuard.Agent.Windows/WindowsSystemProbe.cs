using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AIGuard.Agent.Windows;

/// <summary>Gerçek Windows kaynakları: WMI, kayıt defteri, süreç listesi, user32.</summary>
internal sealed class WindowsSystemProbe(ILogger<WindowsSystemProbe> logger) : IWindowsSystemProbe
{
    private const int SM_REMOTESESSION = 0x1000;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

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

    public bool ServiceExists(string name) => LocalMachineKeyExists(@"SYSTEM\CurrentControlSet\Services\" + name);

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

    public bool IsRemoteSession() => GetSystemMetrics(SM_REMOTESESSION) != 0;

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
