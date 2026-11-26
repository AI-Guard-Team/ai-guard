using AIGuard.Domain.Enums;

namespace AIGuard.Application.Risk;

/// <summary>
/// <c>GET /api/risk-settings</c> yanıtı (UI1, Stitch 29; K-41): riskconfig.json'daki eşik ve olay ağırlıkları, salt okunur.
/// Değerler sunucunun skor hesabında kullandığı değerlerin aynısı; önyüz eşik tahmin etmesin diye buradan okuyor.
/// </summary>
public sealed record RiskSettingsView(
    double SessionThreshold,
    double RepetitionStep,
    double MaxRepetitionMultiplier,
    double DurationScaleSeconds,
    IReadOnlyList<EventRiskRuleView> Events)
{
    public static RiskSettingsView From(RiskOptions options) => new(
        options.SessionThreshold,
        options.RepetitionStep,
        options.MaxRepetitionMultiplier,
        options.DurationScaleSeconds,
        options.Events
            .OrderByDescending(e => e.Value.Weight)
            .ThenBy(e => e.Key.ToString(), StringComparer.Ordinal)
            .Select(e => new EventRiskRuleView(e.Key, e.Value.Weight, e.Value.DecayPerMinute, e.Value.Threshold, e.Value.MaxTotal))
            .ToList());
}

public sealed record EventRiskRuleView(EventType Type, double Weight, double DecayPerMinute, double? Threshold, double? MaxTotal);
