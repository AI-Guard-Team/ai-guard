using AIGuard.Application.Risk;
using AIGuard.Domain.Enums;
using AIGuard.Domain.Events;
using static AIGuard.UnitTests.Risk.RiskTestData;

namespace AIGuard.UnitTests.Risk;

/// <summary>Repodaki riskconfig.json, ayar doğrulaması ve olay kataloğu.</summary>
public class RiskConfigTests
{
    [Fact]
    public void Shipped_config_is_valid()
    {
        var result = new RiskOptionsValidator().Validate(null, ShippedOptions());

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData(EventType.VM_DETECTED, true)]
    [InlineData(EventType.FORBIDDEN_APP, true)]
    [InlineData(EventType.IDENTITY_MISMATCH, true)]
    [InlineData(EventType.AGENT_SIGNAL_INVALID, true)]
    [InlineData(EventType.HYPERVISOR_INFO, false)]
    [InlineData(EventType.MONITORING_GAP, false)]
    public void Shipped_config_flags_only_critical_events_on_their_own(EventType type, bool risky)
    {
        // Tek olay yerine 100 tekrar: bilgi olayları ne kadar tekrarlansa da oturumu riskli yapmamalı.
        var events = Enumerable.Range(0, 100).Select(i => Fake(type, i * 0.1));

        var result = new RiskEngine(ShippedOptions()).Calculate(events, ExamStart.AddMinutes(10));

        Assert.Equal(risky, result.IsRisky);
    }

    [Fact]
    public void Missing_event_type_or_negative_weight_fails_validation()
    {
        var missing = Options();
        missing.Events.Remove(EventType.PHONE);
        var negative = Options();
        negative.Events[EventType.PHONE] = new EventRiskRule { Weight = -1 };

        var validator = new RiskOptionsValidator();

        Assert.Contains("PHONE", validator.Validate(null, missing).FailureMessage);
        Assert.True(validator.Validate(null, negative).Failed);
    }

    [Fact]
    public void Only_the_five_browser_events_can_come_from_the_browser()
    {
        var browser = Enum.GetValues<EventType>().Where(EventCatalog.IsBrowserReportable);

        Assert.Equal(
            [EventType.TAB_BLUR, EventType.FULLSCREEN_EXIT, EventType.COPY_PASTE, EventType.RIGHT_CLICK, EventType.PERMISSION_REVOKED],
            browser);
    }

    [Fact]
    public void Every_event_type_has_a_source_and_a_label()
    {
        Assert.All(Enum.GetValues<EventType>(), type =>
        {
            _ = EventCatalog.SourceOf(type);
            Assert.False(string.IsNullOrWhiteSpace(ReasonFormatter.Label(type)));
        });
    }
}
