using AIGuard.Domain.Enums;
using Microsoft.Extensions.Options;

namespace AIGuard.Application.Risk;

/// <summary>riskconfig.json içeriği (A2). Değerler pilot oturumda ayarlanacak.</summary>
public sealed class RiskOptions
{
    public const string SectionName = "Risk";

    /// <summary>Oturum skoru bu değere ulaşınca oturum Risky işaretleniyor.</summary>
    public double SessionThreshold { get; set; }

    /// <summary>Aynı türün her tekrarında çarpana eklenen pay (2. tekrar 1 + step, 3. tekrar 1 + 2·step …).</summary>
    public double RepetitionStep { get; set; }

    /// <summary>Tekrar çarpanının üst sınırı.</summary>
    public double MaxRepetitionMultiplier { get; set; }

    /// <summary>Süre çarpanındaki ölçek (sn). Süreli olayın çarpanı 1 + ln(1 + süre / ölçek).</summary>
    public double DurationScaleSeconds { get; set; }

    public Dictionary<EventType, EventRiskRule> Events { get; set; } = new();
}

public sealed class EventRiskRule
{
    public double Weight { get; set; }

    /// <summary>Dakika başına üstel azalma katsayısı. 0 ise katkı zamanla azalmıyor.</summary>
    public double DecayPerMinute { get; set; }

    /// <summary>
    /// Bu türün toplam katkısı tek başına bu değere ulaşırsa oturum Risky işaretleniyor
    /// (ör. sanal makine). Boşsa yalnızca oturum eşiği geçerli.
    /// </summary>
    public double? Threshold { get; set; }

    /// <summary>
    /// Bu türün toplam katkısının üst sınırı. Bilgi amaçlı olaylarda (HYPERVISOR_INFO)
    /// çok sayıda tekrarın oturumu tek başına riskli yapmasını önlüyor.
    /// </summary>
    public double? MaxTotal { get; set; }
}

/// <summary>Ayar dosyası hatalıysa uygulama açılışta duruyor.</summary>
public sealed class RiskOptionsValidator : IValidateOptions<RiskOptions>
{
    public ValidateOptionsResult Validate(string? name, RiskOptions options)
    {
        var errors = new List<string>();

        if (options.SessionThreshold <= 0) errors.Add("SessionThreshold 0'dan büyük olmalı.");
        if (options.RepetitionStep < 0) errors.Add("RepetitionStep negatif olamaz.");
        if (options.MaxRepetitionMultiplier < 1) errors.Add("MaxRepetitionMultiplier en az 1 olmalı.");
        if (options.DurationScaleSeconds <= 0) errors.Add("DurationScaleSeconds 0'dan büyük olmalı.");

        foreach (var type in Enum.GetValues<EventType>())
        {
            if (!options.Events.TryGetValue(type, out var rule))
            {
                errors.Add($"{type} için ayar eksik.");
                continue;
            }

            if (rule.Weight < 0) errors.Add($"{type}: Weight negatif olamaz.");
            if (rule.DecayPerMinute < 0) errors.Add($"{type}: DecayPerMinute negatif olamaz.");
            if (rule.Threshold is <= 0) errors.Add($"{type}: Threshold 0'dan büyük olmalı.");
            if (rule.MaxTotal is < 0) errors.Add($"{type}: MaxTotal negatif olamaz.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
