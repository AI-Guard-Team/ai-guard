using Microsoft.Extensions.Options;

namespace AIGuard.Application.Monitoring;

/// <summary>
/// Sınav sırası kamera analizinin eşikleri ve zamanlaması (D4, K-45). Değerler tek yerde; varsayılanlar
/// docs/contracts.md 5. bölümle aynı. Api'de "CameraMonitoring" bölümünden değiştirilebilir.
/// </summary>
public sealed class CameraMonitorOptions
{
    public const string SectionName = "CameraMonitoring";

    /// <summary>FACE_MISSING: art arda bu kadar ölçümde faceCount = 0.</summary>
    public int FaceMissingMeasurements { get; set; } = 3;

    /// <summary>GAZE_AWAY: bir seride bu kadar gazeAway = true ölçümü (araya giren "yüz yok" ölçümleri sayılmıyor).</summary>
    public int GazeAwayMeasurements { get; set; } = 5;

    /// <summary>Telefon kontrolü, son telefon ölçümünden en az bu kadar sonra.</summary>
    public double PhoneIntervalSeconds { get; set; } = 3;

    /// <summary>Kimlik kontrolü, son kimlik ölçümünden (matched true/false) en az bu kadar sonra; oturumun ilk karesinde de.</summary>
    public double IdentityIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Bu süre boyunca başarılı ölçüm gelmezse açık bölümler son ölçüme göre kapatılıyor (kareler kesildi ya da
    /// görüntü servisi uzun süredir yanıt vermiyor).
    /// </summary>
    public double StaleAfterSeconds { get; set; } = 10;

    /// <summary>Bu süre boyunca kare gelmeyen ve açık bölümü olmayan oturumun durumu bellekten siliniyor.</summary>
    public double IdleRemoveAfterSeconds { get; set; } = 120;

    /// <summary>Görüntü servisi çağrısının zaman aşımı; kare isteği bu kadar bekleyebilir.</summary>
    public double VisionTimeoutSeconds { get; set; } = 3;

    /// <summary>Temizlik turunun aralığı (Api'deki arka plan görevi).</summary>
    public double SweepIntervalSeconds { get; set; } = 5;

    /// <summary>Temizlik turu açık mı; testler kapatabiliyor.</summary>
    public bool SweepEnabled { get; set; } = true;

    public TimeSpan PhoneInterval => TimeSpan.FromSeconds(PhoneIntervalSeconds);

    public TimeSpan IdentityInterval => TimeSpan.FromSeconds(IdentityIntervalSeconds);

    public TimeSpan StaleAfter => TimeSpan.FromSeconds(StaleAfterSeconds);

    public TimeSpan IdleRemoveAfter => TimeSpan.FromSeconds(IdleRemoveAfterSeconds);
}

public sealed class CameraMonitorOptionsValidator : IValidateOptions<CameraMonitorOptions>
{
    public ValidateOptionsResult Validate(string? name, CameraMonitorOptions o)
    {
        var errors = new List<string>();
        var p = CameraMonitorOptions.SectionName;

        if (o.FaceMissingMeasurements < 1)
        {
            errors.Add($"{p}:FaceMissingMeasurements en az 1 olmalı.");
        }

        if (o.GazeAwayMeasurements < 1)
        {
            errors.Add($"{p}:GazeAwayMeasurements en az 1 olmalı.");
        }

        if (o.PhoneIntervalSeconds < 0 || o.IdentityIntervalSeconds < 0)
        {
            errors.Add($"{p}: kontrol aralıkları negatif olamaz.");
        }

        if (o.VisionTimeoutSeconds <= 0)
        {
            errors.Add($"{p}:VisionTimeoutSeconds pozitif olmalı.");
        }

        // Zaman aşımına uğrayan bir istek tek başına bölümleri "kesildi" saymamalı.
        if (o.StaleAfterSeconds <= o.VisionTimeoutSeconds)
        {
            errors.Add($"{p}:StaleAfterSeconds, VisionTimeoutSeconds'tan büyük olmalı.");
        }

        // Açık bölümü olan durum silinmesin diye kesilme süresi boşta silme süresinden kısa olmalı.
        if (o.IdleRemoveAfterSeconds <= o.StaleAfterSeconds)
        {
            errors.Add($"{p}:IdleRemoveAfterSeconds, StaleAfterSeconds'tan büyük olmalı.");
        }

        if (o.SweepIntervalSeconds <= 0)
        {
            errors.Add($"{p}:SweepIntervalSeconds pozitif olmalı.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
