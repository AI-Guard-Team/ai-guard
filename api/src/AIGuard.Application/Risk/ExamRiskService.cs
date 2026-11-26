using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AIGuard.Application.Risk;

/// <summary>Bir olay türünün sınavdaki etkin değerleri ve platform varsayılanı (ekranda "değiştirildi" işareti için).</summary>
public sealed record ExamRiskRuleView(
    EventType Type,
    double Weight,
    double DecayPerMinute,
    double? Threshold,
    double DefaultWeight,
    double DefaultDecayPerMinute,
    double? DefaultThreshold,
    bool Customized);

/// <param name="Locked">Sınavda oturum varsa ayarlar değiştirilemez (skorlar adaylar arasında tutarlı kalsın).</param>
public sealed record ExamRiskSettingsView(
    Guid ExamId,
    double SessionThreshold,
    double DefaultSessionThreshold,
    bool Locked,
    IReadOnlyList<ExamRiskRuleView> Events);

public sealed record ExamRiskRuleRequest(EventType? Type, double? Weight, double? DecayPerMinute, double? Threshold);

/// <param name="SessionThreshold">Boşsa platform eşiği.</param>
/// <param name="Events">Yalnızca varsayılandan farklı olan türler; listede olmayan tür varsayılana döner.</param>
public sealed record ExamRiskSettingsRequest(double? SessionThreshold, IReadOnlyList<ExamRiskRuleRequest>? Events);

public enum ExamRiskStatus
{
    Success,
    NotFound,
    Invalid,
    Locked,
}

public sealed record ExamRiskResult(ExamRiskStatus Status, string? Error = null);

/// <summary>
/// Sınava özel risk ayarları (K-49, S-30 kapandı). Platform varsayılanı riskconfig.json'da; sınav yalnızca
/// farklı olan değerleri <see cref="RiskSetting"/> satırlarında ve oturum eşiğini <see cref="Exam.RiskSessionThreshold"/>'da
/// tutuyor. Skor hesaplayan her yer motoru buradan alıyor; böylece canlı skor, inceleme ve rapor aynı ayarla.
/// </summary>
public sealed class ExamRiskService(IAppDbContext db, IOptions<RiskOptions> defaults, TimeProvider clock)
{
    private const double MaxWeight = 100;
    private const double MaxDecay = 5;
    private const double MaxThreshold = 1000;

    /// <summary>Sınavın etkin ayarlarıyla risk motoru; sınava özel ayar yoksa platform varsayılanı.</summary>
    public async Task<RiskEngine> EngineForExamAsync(Guid examId, CancellationToken ct = default)
    {
        var (sessionThreshold, overrides) = await LoadAsync(examId, ct);
        return new RiskEngine(Merge(defaults.Value, sessionThreshold, overrides));
    }

    public async Task<ExamRiskSettingsView?> GetAsync(Guid examId, CancellationToken ct = default)
    {
        if (!await db.Exams.AnyAsync(e => e.Id == examId, ct))
        {
            return null;
        }

        var (sessionThreshold, overrides) = await LoadAsync(examId, ct);
        var effective = Merge(defaults.Value, sessionThreshold, overrides);
        var custom = overrides.Select(o => o.EventType).ToHashSet();
        var locked = await db.ExamSessions.AnyAsync(s => s.ExamId == examId, ct);

        var events = effective.Events
            .OrderByDescending(e => e.Value.Weight)
            .ThenBy(e => e.Key.ToString(), StringComparer.Ordinal)
            .Select(e =>
            {
                var platform = defaults.Value.Events[e.Key];
                return new ExamRiskRuleView(
                    e.Key, e.Value.Weight, e.Value.DecayPerMinute, e.Value.Threshold,
                    platform.Weight, platform.DecayPerMinute, platform.Threshold, custom.Contains(e.Key));
            })
            .ToList();

        return new ExamRiskSettingsView(examId, effective.SessionThreshold, defaults.Value.SessionThreshold, locked, events);
    }

    public async Task<ExamRiskResult> UpdateAsync(Guid actorId, Guid examId, ExamRiskSettingsRequest request, CancellationToken ct = default)
    {
        var exam = await db.Exams.SingleOrDefaultAsync(e => e.Id == examId, ct);
        if (exam is null)
        {
            return new(ExamRiskStatus.NotFound);
        }

        if (request.SessionThreshold is { } threshold && (double.IsNaN(threshold) || threshold < 1 || threshold > MaxThreshold))
        {
            return new(ExamRiskStatus.Invalid, "Oturum eşiği 1–1000 arasında olmalıdır.");
        }

        var rules = request.Events ?? [];
        foreach (var rule in rules)
        {
            if (rule.Type is not { } type || !Enum.IsDefined(type))
            {
                return new(ExamRiskStatus.Invalid, "Bilinmeyen olay türü.");
            }

            if (rule.Weight is not { } weight || double.IsNaN(weight) || weight < 0 || weight > MaxWeight)
            {
                return new(ExamRiskStatus.Invalid, "Olay ağırlığı 0–100 arasında olmalıdır.");
            }

            if (rule.DecayPerMinute is not { } decay || double.IsNaN(decay) || decay < 0 || decay > MaxDecay)
            {
                return new(ExamRiskStatus.Invalid, "Zamanla azalma 0–5 arasında olmalıdır.");
            }

            if (rule.Threshold is { } eventThreshold && (double.IsNaN(eventThreshold) || eventThreshold <= 0 || eventThreshold > MaxThreshold))
            {
                return new(ExamRiskStatus.Invalid, "Olay eşiği boş ya da 0–1000 arasında olmalıdır.");
            }
        }

        if (rules.GroupBy(r => r.Type).Any(g => g.Count() > 1))
        {
            return new(ExamRiskStatus.Invalid, "Aynı olay türü iki kez gönderilmiş.");
        }

        if (await db.ExamSessions.AnyAsync(s => s.ExamId == examId, ct))
        {
            return new(ExamRiskStatus.Locked, "Bu sınavda oturum başlamış; risk ayarları artık değiştirilemez.");
        }

        var existing = await db.RiskSettings.Where(r => r.ExamId == examId).ToListAsync(ct);
        var before = new { sessionThreshold = exam.RiskSessionThreshold, events = existing.Select(Describe).ToList() };

        db.RiskSettings.RemoveRange(existing);
        // Varsayılanla aynı değer satır olarak tutulmuyor: platform varsayılanı değişince sınav da izlesin.
        var platform = defaults.Value;
        var added = rules
            .Where(r =>
            {
                var d = platform.Events[r.Type!.Value];
                return r.Weight != d.Weight || r.DecayPerMinute != d.DecayPerMinute || r.Threshold != d.Threshold;
            })
            .Select(r => new RiskSetting
            {
                ExamId = examId,
                EventType = r.Type!.Value,
                Weight = r.Weight!.Value,
                Decay = r.DecayPerMinute!.Value,
                // Sütun boş olamıyor; 0 "tür eşiği yok" demek (eşik > 0 olmak zorunda).
                Threshold = r.Threshold ?? 0,
            })
            .ToList();
        db.RiskSettings.AddRange(added);
        exam.RiskSessionThreshold = request.SessionThreshold is { } s && s != platform.SessionThreshold ? s : null;

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "exam.risk_settings",
            TargetType = nameof(Exam),
            TargetId = examId,
            OldValue = JsonSerializer.Serialize(before),
            NewValue = JsonSerializer.Serialize(new { sessionThreshold = exam.RiskSessionThreshold, events = added.Select(Describe).ToList() }),
            OccurredAt = clock.GetUtcNow(),
        });

        await db.SaveChangesAsync(ct);
        return new(ExamRiskStatus.Success);
    }

    private async Task<(double? SessionThreshold, List<RiskSetting> Overrides)> LoadAsync(Guid examId, CancellationToken ct)
    {
        var sessionThreshold = await db.Exams.AsNoTracking().Where(e => e.Id == examId).Select(e => e.RiskSessionThreshold).SingleOrDefaultAsync(ct);
        var overrides = await db.RiskSettings.AsNoTracking().Where(r => r.ExamId == examId).ToListAsync(ct);
        return (sessionThreshold, overrides);
    }

    /// <summary>Platform ayarlarının kopyası; sınavın değiştirdiği değerler üstüne yazılmış. Varsayılan nesne değişmez.</summary>
    public static RiskOptions Merge(RiskOptions platform, double? sessionThreshold, IEnumerable<RiskSetting> overrides)
    {
        var events = platform.Events.ToDictionary(
            e => e.Key,
            e => new EventRiskRule { Weight = e.Value.Weight, DecayPerMinute = e.Value.DecayPerMinute, Threshold = e.Value.Threshold, MaxTotal = e.Value.MaxTotal });

        foreach (var o in overrides)
        {
            var rule = events[o.EventType];
            rule.Weight = o.Weight;
            rule.DecayPerMinute = o.Decay;
            rule.Threshold = o.Threshold > 0 ? o.Threshold : null;
        }

        return new RiskOptions
        {
            SessionThreshold = sessionThreshold ?? platform.SessionThreshold,
            RepetitionStep = platform.RepetitionStep,
            MaxRepetitionMultiplier = platform.MaxRepetitionMultiplier,
            DurationScaleSeconds = platform.DurationScaleSeconds,
            Events = events,
        };
    }

    private static object Describe(RiskSetting r) => new { type = r.EventType.ToString(), weight = r.Weight, decay = r.Decay, threshold = r.Threshold };
}
