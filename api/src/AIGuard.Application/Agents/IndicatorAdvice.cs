using AIGuard.Application.Localization;

namespace AIGuard.Application.Agents;

/// <summary>Adaya gösterilecek gösterge: ne bulundu ve nasıl giderilir (Y4, UC-08 4a).</summary>
public sealed record IndicatorAdviceView(string Type, string Name, string Message, string Fix);

/// <summary>
/// Göstergelerin adaya yönelik açıklaması. Metin sunucuda üretiliyor ki önyüz ve ajan aynı dili
/// konuşsun; önyüz yalnızca gösteriyor. İstek İngilizceyse İngilizce (K-48).
/// </summary>
public static class IndicatorAdvice
{
    /// <summary>Gelen Uzak Masaüstü göstergesinin ajandaki adı (Y3, WindowsDeviceInspector).</summary>
    public const string IncomingRemoteDesktopName = "Uzak Masaüstü oturumu";

    private const string Refresh = "Kontrol birkaç saniye içinde kendiliğinden yenilenir.";
    private const string RefreshEn = "The check refreshes by itself within a few seconds.";

    public static IndicatorAdviceView For(AgentIndicator indicator)
    {
        var type = indicator.Type ?? "";
        var name = indicator.Name ?? "";
        var (message, fix) = ApiLanguage.IsEnglish ? English(type, name) : Turkish(type, name);
        return new IndicatorAdviceView(type, name, message, fix);
    }

    private static (string Message, string Fix) Turkish(string type, string name) => type switch
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

    private static (string Message, string Fix) English(string type, string name) => type switch
    {
        "VM_DETECTED" => (
            $"The exam appears to be open inside a virtual machine ({name}).",
            "Take the exam directly on your own computer, outside the virtual machine."),
        "FORBIDDEN_APP" when name == IncomingRemoteDesktopName => (
            "Someone is connected to your computer through Remote Desktop.",
            $"Close the Remote Desktop session and take the exam at the computer itself. {RefreshEn}"),
        "FORBIDDEN_APP" => (
            $"An app that must not be open during the exam is running: {name}.",
            $"Close {name} completely (including its icon in the system tray). {RefreshEn}"),
        "VPN_PROXY" => (
            $"A VPN or proxy server is active: {name}.",
            $"Disconnect the VPN or turn off the proxy setting in Windows settings. {RefreshEn}"),
        _ => (
            $"An indicator was found on the device: {name}.",
            RefreshEn),
    };
}
