using System.Diagnostics;
using System.Management;
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
}
