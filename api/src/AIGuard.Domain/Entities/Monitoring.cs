using AIGuard.Domain.Common;
using AIGuard.Domain.Enums;

namespace AIGuard.Domain.Entities;

/// <summary>
/// Kamera, ses, tarayıcı, ajan ve ağ olaylarının ortak kaydı. Risk motoru yalnızca bu
/// tabloyu okuyor; yeni bir kaynak eklemek motoru değiştirmeyi gerektirmiyor.
/// </summary>
public class Event : Entity
{
    public Guid ExamSessionId { get; set; }

    public ExamSession ExamSession { get; set; } = null!;

    public EventType Type { get; set; }

    public EventSource Source { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public double DurationSec { get; set; }

    /// <summary>Model güven skoru (0–1); tarayıcı ve ajan olaylarında boş.</summary>
    public double? Confidence { get; set; }

    /// <summary>
    /// Kaynağa özgü ek bilgi (jsonb). Ham kare, ses ya da tam süreç listesi yazılmamalı.
    /// </summary>
    public string? Payload { get; set; }

    public double ScoreContribution { get; set; }

    /// <summary>Gerekçe metni, örn. "12. dakikada 8 sn telefon görüldü".</summary>
    public string? Reason { get; set; }

    /// <summary>Olayın sunucuda kaydedildiği an. A6'daki gecikme ölçümünde kullanılıyor.</summary>
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    // Yanlış alarm işaretlenen olay silinmiyor (A4).
    public bool IsFalseAlarm { get; set; }

    public Guid? FalseAlarmMarkedById { get; set; }

    public User? FalseAlarmMarkedBy { get; set; }

    public DateTimeOffset? FalseAlarmMarkedAt { get; set; }

    public EvidenceFrame? EvidenceFrame { get; set; }
}

/// <summary>
/// Kanıt karesinin kaydı. Görüntü şifreli olarak dosya deposunda duruyor; tabloda yalnızca
/// konumu var. Büyük dosyaları veritabanı yedeklerinin dışında tutmak için böyle yaptık.
/// </summary>
public class EvidenceFrame : Entity
{
    public Guid EventId { get; set; }

    public Event Event { get; set; } = null!;

    /// <summary>Depodaki dosya adı. Sunucu üretiyor, kullanıcı girdisinden oluşturulmamalı.</summary>
    public required string StorageKey { get; set; }

    public int KeyVersion { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Saklama süresinin bittiği an; A5'teki silme görevi bu alana bakıyor.</summary>
    public DateTimeOffset DeleteAfter { get; set; }
}

/// <summary>
/// Olay türüne göre risk ayarı. Varsayılanlar riskconfig.json'dan okunuyor (A2);
/// bu tablo sınav bazında ayar için. ExamId boşsa sistem geneli kayıt.
/// </summary>
public class RiskSetting : Entity
{
    public Guid? ExamId { get; set; }

    public Exam? Exam { get; set; }

    public EventType EventType { get; set; }

    public double Weight { get; set; }

    public double Decay { get; set; }

    public double Threshold { get; set; }
}
