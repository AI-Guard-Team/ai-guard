using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AIGuard.Agent.MacOS;

/// <summary>
/// <c>sysctl</c>, <c>system_profiler</c> ve <c>ioreg</c> okuması (Y5). Yönetici izni istemiyor.
/// </summary>
/// <remarks>
/// Çıktının tamamı loglanmıyor: <c>system_profiler</c> seri numarası da basıyor. Yalnızca model
/// alanları ayrıştırılıyor. Komut hata verirse ajan durmuyor; ilgili alan null kalıyor.
/// </remarks>
internal sealed class MacSystemProbe(ILogger<MacSystemProbe> logger) : IMacSystemProbe
{
    public MacHardware ReadHardware()
    {
        try
        {
            var sysctl = Run("/usr/sbin/sysctl", ["-n", "kern.hv_vmm_present"], milliseconds: 3_000);
            var profiler = Run("/usr/sbin/system_profiler", ["SPHardwareDataType"], milliseconds: 8_000);
            var ioreg = Run("/usr/sbin/ioreg", ["-rd1", "-c", "IOPlatformExpertDevice"], milliseconds: 3_000);

            return new MacHardware(
                HypervisorPresent: sysctl?.Trim() switch
                {
                    "1" => true,
                    "0" => false,
                    _ => null,
                },
                ModelName: Field(profiler, "Model Name"),
                ModelIdentifier: Field(profiler, "Model Identifier"),
                IoManufacturer: IoregValue(ioreg, "manufacturer"),
                IoProduct: IoregValue(ioreg, "product-name"),
                IoModel: IoregValue(ioreg, "model"));
        }
        catch (Exception ex)
        {
            logger.LogWarning("Donanım bilgisi okunamadı ({Error}); sanal makine göstergesi üretilmedi.", ex.Message);
            return new MacHardware();
        }
    }

    /// <summary><c>system_profiler</c> satırı: "Model Name: MacBook Air".</summary>
    internal static string? Field(string? text, string label)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var prefix = label + ":";
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = line[prefix.Length..].Trim();
                return value.Length == 0 ? null : value;
            }
        }

        return null;
    }

    /// <summary><c>ioreg</c> değeri: <c>"model" = &lt;"Mac14,2"&gt;</c>. Tırnaksız (ham bayt) değer alınmıyor.</summary>
    internal static string? IoregValue(string? text, string key)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var marker = "\"" + key + "\"";
        var idx = text.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return null;
        }

        var eq = text.IndexOf('=', idx + marker.Length);
        if (eq < 0)
        {
            return null;
        }

        var rest = text.AsSpan(eq + 1).TrimStart();
        if (rest.StartsWith("<"))
        {
            rest = rest[1..].TrimStart();
        }

        if (rest.IsEmpty || rest[0] != '"')
        {
            return null;
        }

        rest = rest[1..];
        var end = rest.IndexOf('"');
        if (end <= 0)
        {
            return null;
        }

        return rest[..end].ToString();
    }

    private string? Run(string file, string[] args, int milliseconds)
    {
        var name = Path.GetFileName(file);
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = file,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var arg in args)
            {
                process.StartInfo.ArgumentList.Add(arg);
            }

            if (!process.Start())
            {
                logger.LogWarning("{Command} başlatılamadı.", name);
                return null;
            }

            // Tampon dolup komutun takılmaması için iki akış da okunuyor; içerik loglanmıyor.
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(milliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    logger.LogWarning("{Command} durdurulamadı ({Error}).", name, ex.Message);
                }

                logger.LogWarning("{Command} zaman aşımına uğradı.", name);
                return null;
            }

            if (!stdout.Wait(1_000) || process.ExitCode != 0)
            {
                logger.LogWarning("{Command} çıkış kodu {Code}.", name, process.ExitCode);
                return null;
            }

            stderr.Wait(1_000);
            return stdout.Result.Trim();
        }
        catch (Exception ex)
        {
            logger.LogWarning("{Command} çalıştırılamadı ({Error}).", name, ex.Message);
            return null;
        }
    }
}
