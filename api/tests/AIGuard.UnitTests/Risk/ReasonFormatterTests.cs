using AIGuard.Application.Risk;
using AIGuard.Domain.Enums;
using static AIGuard.UnitTests.Risk.RiskTestData;

namespace AIGuard.UnitTests.Risk;

public class ReasonFormatterTests
{
    [Theory]
    [InlineData(5, 0, "1. dakikada konuşma algılandı")] // ilk dakika 0 değil 1
    [InlineData(690, 8, "12. dakikada 8 sn konuşma algılandı")]
    public void Minute_is_counted_from_exam_start(int secondsAfterStart, double duration, string expected)
    {
        var reason = ReasonFormatter.Format(EventType.SPEECH, ExamStart.AddSeconds(secondsAfterStart), duration, ExamStart);

        Assert.Equal(expected, reason);
    }

    [Fact]
    public void Events_before_the_exam_starts_are_described_as_such()
    {
        var reason = ReasonFormatter.Format(EventType.VERIFICATION_FAILED, ExamStart, 0, examStartedAt: null);

        Assert.Equal("Sınav başlamadan önce kimlik doğrulama başarısız oldu", reason);
    }
}
