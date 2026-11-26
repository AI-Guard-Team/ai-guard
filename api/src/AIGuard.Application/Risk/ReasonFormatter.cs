using System.Globalization;
using System.Text.RegularExpressions;
using AIGuard.Application.Localization;
using AIGuard.Domain.Enums;

namespace AIGuard.Application.Risk;

/// <summary>
/// Değerlendiricinin göreceği Türkçe gerekçe metni, ör. "12. dakikada 8 sn telefon görüldü".
/// </summary>
public static partial class ReasonFormatter
{
    private static readonly Dictionary<EventType, string> EnglishLabels = new()
    {
        [EventType.FACE_MISSING] = "face not detected",
        [EventType.MULTIPLE_FACES] = "more than one face seen",
        [EventType.GAZE_AWAY] = "looked away from the screen",
        [EventType.PHONE] = "phone seen",
        [EventType.IDENTITY_MISMATCH] = "face did not match the reference photo",
        [EventType.VERIFICATION_FAILED] = "identity verification failed",
        [EventType.SPEECH] = "speech detected",
        [EventType.TAB_BLUR] = "left the exam tab",
        [EventType.FULLSCREEN_EXIT] = "left full screen",
        [EventType.COPY_PASTE] = "copy/paste used",
        [EventType.RIGHT_CLICK] = "right-clicked",
        [EventType.PERMISSION_REVOKED] = "camera or microphone permission revoked",
        [EventType.VM_DETECTED] = "virtual machine detected",
        [EventType.HYPERVISOR_INFO] = "hypervisor information received (informational)",
        [EventType.FORBIDDEN_APP] = "a forbidden app was running",
        [EventType.VPN_PROXY] = "VPN or proxy detected",
        [EventType.IP_CHANGE] = "IP address changed",
        [EventType.AGENT_SIGNAL_LOST] = "no signal from the security agent",
        [EventType.AGENT_SIGNAL_INVALID] = "invalid signal from the security agent",
        [EventType.CONNECTION_LOST] = "connection lost",
        [EventType.MONITORING_GAP] = "monitoring service did not respond",
    };

    private static readonly Dictionary<EventType, string> Labels = new()
    {
        [EventType.FACE_MISSING] = "yüz algılanamadı",
        [EventType.MULTIPLE_FACES] = "birden fazla yüz görüldü",
        [EventType.GAZE_AWAY] = "ekrandan başka yöne bakıldı",
        [EventType.PHONE] = "telefon görüldü",
        [EventType.IDENTITY_MISMATCH] = "yüz kayıt fotoğrafıyla eşleşmedi",
        [EventType.VERIFICATION_FAILED] = "kimlik doğrulama başarısız oldu",
        [EventType.SPEECH] = "konuşma algılandı",
        [EventType.TAB_BLUR] = "sınav sekmesinden çıkıldı",
        [EventType.FULLSCREEN_EXIT] = "tam ekrandan çıkıldı",
        [EventType.COPY_PASTE] = "kopyala/yapıştır yapıldı",
        [EventType.RIGHT_CLICK] = "sağ tıklandı",
        [EventType.PERMISSION_REVOKED] = "kamera veya mikrofon izni kaldırıldı",
        [EventType.VM_DETECTED] = "sanal makine tespit edildi",
        [EventType.HYPERVISOR_INFO] = "hipervizör bilgisi alındı (bilgi amaçlı)",
        [EventType.FORBIDDEN_APP] = "yasaklı uygulama çalışıyordu",
        [EventType.VPN_PROXY] = "VPN veya vekil sunucu tespit edildi",
        [EventType.IP_CHANGE] = "IP adresi değişti",
        [EventType.AGENT_SIGNAL_LOST] = "güvenlik ajanından sinyal alınamadı",
        [EventType.AGENT_SIGNAL_INVALID] = "güvenlik ajanından geçersiz sinyal geldi",
        [EventType.CONNECTION_LOST] = "bağlantı kesildi",
        [EventType.MONITORING_GAP] = "izleme servisi yanıt vermedi",
    };

    public static string Label(EventType type) => Labels[type];

    /// <param name="examStartedAt">Oturumun InProgress'e geçtiği an; sınav başlamadıysa boş.</param>
    public static string Format(EventType type, DateTimeOffset startedAt, double durationSec, DateTimeOffset? examStartedAt)
    {
        var when = examStartedAt is { } start && startedAt >= start
            ? $"{(int)Math.Floor((startedAt - start).TotalMinutes) + 1}. dakikada"
            : "Sınav başlamadan önce";

        var duration = durationSec >= 1
            ? $" {Math.Round(durationSec).ToString(CultureInfo.InvariantCulture)} sn"
            : string.Empty;

        return $"{when}{duration} {Labels[type]}";
    }

    [GeneratedRegex(@"^(?:(\d+)\. dakikada|Sınav başlamadan önce)(?: (\d+) sn)? (.+)$")]
    private static partial Regex StoredReason();

    /// <summary>
    /// Olayla birlikte Türkçe saklanan gerekçeyi istek İngilizceyse çevirir (K-48). Bu formatlayıcının kalıbına
    /// uymayan metin olduğu gibi döner; saklanan kayıt değişmez.
    /// </summary>
    public static string? Localize(string? stored)
    {
        if (stored is null || !ApiLanguage.IsEnglish)
        {
            return stored;
        }

        var match = StoredReason().Match(stored);
        if (!match.Success)
        {
            return stored;
        }

        var label = Labels.FirstOrDefault(pair => pair.Value == match.Groups[3].Value);
        if (label.Value is null)
        {
            return stored;
        }

        var when = match.Groups[1].Success ? $"In minute {match.Groups[1].Value}" : "Before the exam started";
        var duration = match.Groups[2].Success ? $", for {match.Groups[2].Value} s," : ",";
        return $"{when}{duration} {EnglishLabels[label.Key]}";
    }
}
