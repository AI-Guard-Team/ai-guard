using AIGuard.Application.Risk;
using AIGuard.Domain.Enums;
using static AIGuard.UnitTests.Risk.RiskTestData;

namespace AIGuard.UnitTests.Risk;

public class RiskEngineTests
{
    private readonly RiskEngine _engine = new(Options());

    [Fact]
    public void Acceptance_scenario_matches_expected_score_contributions_and_reasons()
    {
        // A2 kabul kriteri. 11. dk'da 8 sn telefon, 15. ve 16. dk'da sekme değişimi; değerlendirme 20. dk'da.
        var phone = Fake(EventType.PHONE, 11, durationSec: 8);
        var tab1 = Fake(EventType.TAB_BLUR, 15);
        var tab2 = Fake(EventType.TAB_BLUR, 16);

        var result = _engine.Calculate([tab2, phone, tab1], ExamStart.AddMinutes(20));

        // Telefon: 5 × (1 + ln 1,8), bitişinden 8,8667 dk sonra × e^(−0,02 × 8,8667)
        // Sekme 1: 1,5 × e^(−0,05 × 5); sekme 2 ikinci tekrar: 1,5 × 1,25 × e^(−0,05 × 4)
        Assert.Equal(6.6489, Contribution(result, phone), 4);
        Assert.Equal(1.1682, Contribution(result, tab1), 4);
        Assert.Equal(1.5351, Contribution(result, tab2), 4);
        Assert.Equal(9.3522, result.Score, 4);
        Assert.False(result.IsRisky);
        Assert.Equal("12. dakikada 8 sn telefon görüldü",
            ReasonFormatter.Format(phone.Type, phone.StartedAt, phone.DurationSec, ExamStart));
    }

    [Fact]
    public void Repetition_multiplier_grows_and_is_capped()
    {
        var events = Enumerable.Range(0, 7).Select(i => Fake(EventType.VPN_PROXY, i));

        var result = _engine.Calculate(events, ExamStart.AddHours(1));

        Assert.Equal([1, 1.25, 1.5, 1.75, 2, 2, 2], result.Contributions.Select(c => c.BaseContribution));
    }

    [Fact]
    public void Contribution_decays_from_the_end_of_the_event()
    {
        var e = Fake(EventType.PHONE, 0, durationSec: 60);

        var result = _engine.Calculate([e], e.StartedAt.AddMinutes(31));

        Assert.Equal(5 * (1 + Math.Log(7)) * Math.Exp(-0.02 * 30), result.Score, 4);
    }

    [Fact]
    public void False_alarms_and_future_events_do_not_count()
    {
        var events = new[]
        {
            Fake(EventType.PHONE, 0),
            Fake(EventType.PHONE, 1, falseAlarm: true),
            Fake(EventType.PHONE, 10),
        };

        var result = _engine.Calculate(events, ExamStart.AddMinutes(5));

        Assert.Single(result.Contributions);
    }

    [Fact]
    public void Session_threshold_and_type_threshold_both_flag_the_session()
    {
        var bySum = _engine.Calculate([Fake(EventType.PHONE, 0), Fake(EventType.PHONE, 0)], ExamStart);
        var byType = _engine.Calculate([Fake(EventType.VM_DETECTED, 0)], ExamStart.AddMinutes(1));

        Assert.True(bySum.IsRisky);
        Assert.Empty(bySum.TypesOverThreshold);
        Assert.True(byType.IsRisky);
        Assert.Equal([EventType.VM_DETECTED], byType.TypesOverThreshold);
    }

    [Fact]
    public void Score_just_below_threshold_is_not_rounded_into_risky()
    {
        var options = Options();
        options.Events[EventType.SPEECH] = new EventRiskRule { Weight = 9.99996 };

        var result = new RiskEngine(options).Calculate([Fake(EventType.SPEECH, 0)], ExamStart);

        Assert.False(result.IsRisky);
    }

    [Fact]
    public void Hypervisor_info_is_capped_and_never_flags_alone()
    {
        var events = Enumerable.Range(0, 1000).Select(i => Fake(EventType.HYPERVISOR_INFO, i * 0.05));

        var result = _engine.Calculate(events, ExamStart.AddHours(2));

        Assert.Equal(1, result.Score, 4);
        Assert.False(result.IsRisky);
    }

    private static double Contribution(RiskResult result, RiskEventInput e) =>
        result.Contributions.Single(c => c.EventId == e.EventId).Contribution;
}
