using AIGuard.Application.Exams;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;

namespace AIGuard.UnitTests.Exams;

/// <summary>Erişim penceresi, son başlangıç zamanı, tek oturum hakkı ve alan kuralları (E3, FR-06, FR-07).</summary>
public class ExamRulesTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    // GAB örneği: 10–12 Kasım penceresi, 60 dakikalık sınav.
    private static Exam Exam() => new()
    {
        Name = "Test", Type = ExamType.EnglishProficiency, DurationMinutes = 60,
        WindowStartAt = Start, WindowEndAt = Start.AddDays(2),
    };

    private static ExamAssignment Candidate(Exam exam, DateTimeOffset? used = null) =>
        new() { ExamId = exam.Id, Role = AssignmentRole.Candidate, AttemptUsedAt = used };

    private static ExamRequest Request(int duration = 60, int? retention = 30, double windowHours = 48) =>
        new("Sınav", ExamType.GeneralAptitude, duration, Start, Start.AddHours(windowHours), retention);

    [Fact]
    public void Latest_start_is_window_end_minus_duration()
    {
        Assert.Equal(Start.AddDays(2).AddMinutes(-60), ExamRules.LatestStartAt(Exam()));
    }

    [Fact]
    public void Candidate_can_start_any_time_inside_the_window_until_the_latest_start()
    {
        var exam = Exam();
        var latest = ExamRules.LatestStartAt(exam);

        Assert.Null(ExamRules.CanStart(exam, Candidate(exam), Start));
        Assert.Null(ExamRules.CanStart(exam, Candidate(exam), latest));
        Assert.Equal(StartDenial.TooLate, ExamRules.CanStart(exam, Candidate(exam), latest.AddSeconds(1)));
        Assert.Equal(StartDenial.WindowNotOpen, ExamRules.CanStart(exam, Candidate(exam), Start.AddSeconds(-1)));
    }

    [Fact]
    public void Single_attempt_and_assignment_are_required()
    {
        var exam = Exam();

        Assert.Equal(StartDenial.AttemptUsed, ExamRules.CanStart(exam, Candidate(exam, used: Start), Start.AddHours(1)));
        Assert.Equal(StartDenial.NotAssigned, ExamRules.CanStart(exam, null, Start.AddHours(1)));
        Assert.Equal(StartDenial.NotAssigned, ExamRules.CanStart(exam,
            new ExamAssignment { ExamId = exam.Id, Role = AssignmentRole.Reviewer }, Start.AddHours(1)));
        Assert.Equal(StartDenial.NotAssigned, ExamRules.CanStart(exam,
            new ExamAssignment { ExamId = Guid.NewGuid(), Role = AssignmentRole.Candidate }, Start.AddHours(1)));
    }

    [Fact]
    public void Valid_exam_request_passes()
    {
        Assert.Null(ExamRules.Validate(Request()));
        Assert.Null(ExamRules.Validate(Request(retention: null))); // varsayılan 30 gün
    }

    [Theory]
    [InlineData(0, 30, 48)]     // süre 0
    [InlineData(601, 30, 48)]   // süre üst sınırın üstünde
    [InlineData(60, 0, 48)]     // saklama 0
    [InlineData(60, 30, 0)]     // bitiş = başlangıç
    [InlineData(90, 30, 1)]     // pencere sınav süresinden kısa
    public void Invalid_exam_requests_are_rejected(int duration, int retention, double windowHours)
    {
        Assert.NotNull(ExamRules.Validate(Request(duration, retention, windowHours)));
    }

    [Fact]
    public void Exam_needs_name_and_type()
    {
        Assert.NotNull(ExamRules.Validate(Request() with { Name = "  " }));
        Assert.NotNull(ExamRules.Validate(Request() with { Type = null }));
        Assert.NotNull(ExamRules.Validate(Request() with { Type = (ExamType)99 }));
    }

    [Theory]
    [InlineData(new[] { "A", "B" }, 1, true)]
    [InlineData(new[] { "A" }, 0, false)]           // tek seçenek
    [InlineData(new[] { "A", "B" }, 2, false)]      // doğru seçenek aralık dışında
    [InlineData(new[] { "A", "B" }, -1, false)]
    [InlineData(new[] { "A", " " }, 0, false)]      // boş seçenek
    public void Question_rules(string[] options, int correct, bool valid)
    {
        Assert.Equal(valid, ExamRules.Validate(new QuestionRequest("Soru?", options, correct, null)) is null);
    }

    [Fact]
    public void Question_needs_text_and_at_most_ten_options()
    {
        Assert.NotNull(ExamRules.Validate(new QuestionRequest("", ["A", "B"], 0, null)));
        Assert.NotNull(ExamRules.Validate(new QuestionRequest("Soru?", Enumerable.Range(0, 11).Select(i => $"{i}").ToList(), 0, null)));
        Assert.NotNull(ExamRules.Validate(new QuestionRequest("Soru?", ["A", "B"], 0, -1)));
    }
}
