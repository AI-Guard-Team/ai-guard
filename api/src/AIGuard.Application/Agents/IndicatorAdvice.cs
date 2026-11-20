namespace AIGuard.Application.Agents;

/// <summary>Adaya gösterilecek gösterge: ne bulundu ve nasıl giderilir (Y4, UC-08 4a).</summary>
public sealed record IndicatorAdviceView(string Type, string Name, string Message, string Fix);

/// <summary>
/// Göstergelerin adaya yönelik açıklaması. Metin sunucuda üretiliyor ki önyüz ve ajan aynı dili
/// konuşsun; önyüz yalnızca gösteriyor.
/// </summary>
public static class IndicatorAdvice
{
    /// <summary>Gelen Uzak Masaüstü göstergesinin ajandaki adı (Y3, WindowsDeviceInspector).</summary>
    public const string IncomingRemoteDesktopName = "Uzak Masaüstü oturumu";

    private const string Refresh = "Kontrol birkaç saniye içinde kendiliğinden yenilenir.";

    public static IndicatorAdviceView For(AgentIndicator indicator)
    {
        var type = indicator.Type ?? "";
        var name = indicator.Name ?? "";

        var (message, fix) = type switch
        {
            "VM_DETECTED" => (
                $"Sınav bir sanal makinenin içinde açılmış görünüyor ({name}).",
                "Sınava sanal makine dışında, doğrudan kendi bilgisayarınızdan girin."),
            "FORBIDDEN_APP" when name == IncomingRemoteDesktopName => (
                "Bilgisayarınıza Uzak Masaüstü ile bağlanılmış.",
                $"Uzak Masaüstü oturumunu kapatıp sınava bilgisayarın başından girin. {Refresh}"),
            "FORBIDDEN_APP" => (
                $"Sınav sırasında açık olmaması gereken bir uygulama çalışıyor: {name}.",
                $"{name} uygulamasını tamamen kapatın (sistem tepsisindeki simgesi dahil). {Refresh}"),
            "VPN_PROXY" => (
                $"VPN veya vekil sunucu etkin: {name}.",
                $"VPN bağlantısını kesin ya da Windows ayarlarındaki vekil sunucu (proxy) ayarını kapatın. {Refresh}"),
            _ => (
                $"Cihazda bir gösterge bulundu: {name}.",
                Refresh),
        };

        return new IndicatorAdviceView(type, name, message, fix);
    }
}
