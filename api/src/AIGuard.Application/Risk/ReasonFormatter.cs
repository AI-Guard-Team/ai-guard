using System.Globalization;
using AIGuard.Domain.Enums;

namespace AIGuard.Application.Risk;

/// <summary>
/// Değerlendiricinin göreceği Türkçe gerekçe metni, ör. "12. dakikada 8 sn telefon görüldü".
/// </summary>
public static class ReasonFormatter
{
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
}
