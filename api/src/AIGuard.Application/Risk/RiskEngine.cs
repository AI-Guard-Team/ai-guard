using AIGuard.Domain.Enums;

namespace AIGuard.Application.Risk;

/// <summary>Risk hesabına giren olay bilgisi.</summary>
public sealed record RiskEventInput(
    Guid EventId,
    EventType Type,
    DateTimeOffset StartedAt,
    double DurationSec,
    DateTimeOffset ReceivedAt,
    bool IsFalseAlarm = false);

/// <summary>Bir olayın skora katkısı.</summary>
/// <param name="BaseContribution">Azalma uygulanmadan önceki katkı (ağırlık × süre × tekrar).</param>
/// <param name="Contribution">Değerlendirme anındaki katkı (azalma ve tür üst sınırı uygulanmış).</param>
public sealed record EventContribution(
    Guid EventId,
    EventType Type,
    int Repetition,
    double BaseContribution,
    double Contribution);

public sealed record RiskResult(
    double Score,
    bool IsRisky,
    IReadOnlyList<EventContribution> Contributions,
    IReadOnlyList<EventType> TypesOverThreshold);

/// <summary>
/// Olaylardan risk skorunu hesaplıyor. Veritabanına dokunmadığı için aynı girdi her
/// zaman aynı sonucu veriyor; skor istenen her an olaylardan yeniden hesaplanabiliyor.
/// </summary>
/// <remarks>
/// katkı = ağırlık × süreÇarpanı × tekrarÇarpanı × e^(−azalma × geçen dakika)
/// <list type="bullet">
/// <item>süreÇarpanı: anlık olayda 1, süreli olayda 1 + ln(1 + süre / ölçek). Uzun olaylar daha ağır ama doğrusal büyümüyor.</item>
/// <item>tekrarÇarpanı: aynı türün n. tekrarında min(1 + adım × (n − 1), üst sınır).</item>
/// <item>geçen dakika: olayın bitişinden değerlendirme anına kadar.</item>
/// </list>
/// Yanlış alarm işaretli olaylar skora girmiyor. Ayrıntılı gerekçe: K-17.
/// </remarks>
public sealed class RiskEngine(RiskOptions options)
{
    public RiskResult Calculate(IEnumerable<RiskEventInput> events, DateTimeOffset at)
    {
        // Tekrar sırası olayın başladığı ana göre; eşitlikte sunucuya ulaşma sırası.
        var ordered = events
            .Where(e => !e.IsFalseAlarm && e.StartedAt <= at)
            .OrderBy(e => e.StartedAt)
            .ThenBy(e => e.ReceivedAt)
            .ThenBy(e => e.EventId)
            .ToList();

        var repetitions = new Dictionary<EventType, int>();
        var contributions = new List<EventContribution>(ordered.Count);

        foreach (var e in ordered)
        {
            var rule = options.Events[e.Type];
            var n = repetitions[e.Type] = repetitions.GetValueOrDefault(e.Type) + 1;

            var baseValue = rule.Weight * DurationFactor(e.DurationSec) * RepetitionFactor(n);
            var endedAt = e.StartedAt.AddSeconds(e.DurationSec);
            var minutes = Math.Max(0, (at - endedAt).TotalMinutes);
            var decayed = baseValue * Math.Exp(-rule.DecayPerMinute * minutes);

            contributions.Add(new EventContribution(e.EventId, e.Type, n, baseValue, decayed));
        }

        ApplyTypeCaps(contributions);

        var totalsByType = contributions
            .GroupBy(c => c.Type)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Contribution));

        var overThreshold = totalsByType
            .Where(t => options.Events[t.Key].Threshold is { } limit && t.Value >= limit)
            .Select(t => t.Key)
            .OrderBy(t => t)
            .ToList();

        // Eşik kararı yuvarlanmamış değerle; yuvarlama yalnızca saklanan/gösterilen skor için.
        var score = totalsByType.Values.Sum();
        var isRisky = score >= options.SessionThreshold || overThreshold.Count > 0;

        return new RiskResult(Math.Round(score, 4), isRisky, contributions, overThreshold);
    }

    private double DurationFactor(double durationSec) =>
        durationSec <= 0 ? 1 : 1 + Math.Log(1 + durationSec / options.DurationScaleSeconds);

    private double RepetitionFactor(int n) =>
        Math.Min(1 + options.RepetitionStep * (n - 1), options.MaxRepetitionMultiplier);

    // MaxTotal tanımlı türlerde toplam katkı sınırı aşılınca kalan olayların katkısı kırpılıyor.
    private void ApplyTypeCaps(List<EventContribution> contributions)
    {
        var used = new Dictionary<EventType, double>();

        for (var i = 0; i < contributions.Count; i++)
        {
            var c = contributions[i];
            if (options.Events[c.Type].MaxTotal is not { } cap)
            {
                continue;
            }

            var remaining = Math.Max(0, cap - used.GetValueOrDefault(c.Type));
            var allowed = Math.Min(c.Contribution, remaining);
            used[c.Type] = used.GetValueOrDefault(c.Type) + allowed;
            contributions[i] = c with { Contribution = allowed };
        }
    }
}
