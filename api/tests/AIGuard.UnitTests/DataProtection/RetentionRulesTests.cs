using AIGuard.Application.DataProtection;
using AIGuard.Domain.Enums;

namespace AIGuard.UnitTests.DataProtection;

/// <summary>A5: saklama süresinin hangi andan sayıldığı (K-32).</summary>
public class RetentionRulesTests
{
    private static readonly DateTimeOffset Created = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Ended = Created.AddHours(1);
    private static readonly DateTimeOffset WindowEnd = Created.AddDays(3);

    [Theory]
    [InlineData(SessionState.Completed)]
    [InlineData(SessionState.Decided)]
    [InlineData(SessionState.Cancelled)]
    public void Closed_session_counts_from_its_end(SessionState state) =>
        Assert.Equal(Ended, DataRetentionService.RetentionStart(state, Ended, Created, WindowEnd));

    [Fact]
    public void Closed_session_without_end_time_counts_from_creation() =>
        Assert.Equal(Created, DataRetentionService.RetentionStart(SessionState.Cancelled, null, Created, WindowEnd));

    [Theory]
    [InlineData(SessionState.ConsentPending)]
    [InlineData(SessionState.AgentPairing)]
    [InlineData(SessionState.IdentityVerification)]
    public void Session_that_never_started_counts_from_the_exam_window_end(SessionState state) =>
        Assert.Equal(WindowEnd, DataRetentionService.RetentionStart(state, null, Created, WindowEnd));

    [Fact]
    public void Periods_follow_the_data_life_cycle_table()
    {
        // GAB 3.3.8: yüz vektörü son sınavdan 30 gün, rıza ve denetim kayıtları 1 yıl.
        Assert.Equal(TimeSpan.FromDays(30), DataRetentionService.FaceTemplateRetention);
        Assert.Equal(TimeSpan.FromDays(365), DataRetentionService.RecordRetention);
    }
}
