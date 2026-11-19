using AIGuard.Application.Risk;
using AIGuard.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace AIGuard.UnitTests.Risk;

/// <summary>Risk testleri için ayarlar ve sahte olay üreticisi.</summary>
internal static class RiskTestData
{
    public static readonly DateTimeOffset ExamStart = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Testlerde kullanılan sabit ayarlar. riskconfig.json pilot oturumda değişebileceği için
    /// beklenen değerler bu ayarlara göre hesaplandı.
    /// </summary>
    public static RiskOptions Options()
    {
        var options = new RiskOptions
        {
            SessionThreshold = 10,
            RepetitionStep = 0.25,
            MaxRepetitionMultiplier = 2.0,
            DurationScaleSeconds = 10,
        };

        foreach (var type in Enum.GetValues<EventType>())
        {
            options.Events[type] = new EventRiskRule { Weight = 1, DecayPerMinute = 0 };
        }

        options.Events[EventType.PHONE] = new EventRiskRule { Weight = 5, DecayPerMinute = 0.02 };
        options.Events[EventType.TAB_BLUR] = new EventRiskRule { Weight = 1.5, DecayPerMinute = 0.05 };
        options.Events[EventType.VM_DETECTED] = new EventRiskRule { Weight = 10, Threshold = 10 };
        options.Events[EventType.HYPERVISOR_INFO] = new EventRiskRule { Weight = 0.5, MaxTotal = 1 };
        return options;
    }

    /// <summary>Repodaki riskconfig.json dosyasını okuyor.</summary>
    public static RiskOptions ShippedOptions()
    {
        var path = FindRepoFile(Path.Combine("src", "AIGuard.Api", "riskconfig.json"));
        var options = new RiskOptions();
        new ConfigurationBuilder().AddJsonFile(path).Build().GetSection(RiskOptions.SectionName).Bind(options);
        return options;
    }

    /// <summary>Sahte olay: sınav başlangıcından <paramref name="minute"/> dakika sonra.</summary>
    public static RiskEventInput Fake(EventType type, double minute, double durationSec = 0, bool falseAlarm = false)
    {
        var started = ExamStart.AddMinutes(minute);
        return new RiskEventInput(Guid.NewGuid(), type, started, durationSec, started.AddSeconds(durationSec), falseAlarm);
    }

    private static string FindRepoFile(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"{relative} bulunamadı.");
    }
}
