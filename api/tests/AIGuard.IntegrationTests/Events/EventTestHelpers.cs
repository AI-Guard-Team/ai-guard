using AIGuard.Application.Events;
using AIGuard.Application.Risk;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Persistence;
using Microsoft.Extensions.Time.Testing;

namespace AIGuard.IntegrationTests.Events;

internal static class EventTestHelpers
{
    public static readonly DateTimeOffset Now = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    public static RiskOptions Options()
    {
        var options = new RiskOptions
        {
            SessionThreshold = 10,
            RepetitionStep = 0.25,
            MaxRepetitionMultiplier = 2,
            DurationScaleSeconds = 10,
        };

        foreach (var type in Enum.GetValues<EventType>())
        {
            options.Events[type] = new EventRiskRule { Weight = 1, DecayPerMinute = 0.05 };
        }

        options.Events[EventType.PHONE] = new EventRiskRule { Weight = 5, DecayPerMinute = 0.02 };
        options.Events[EventType.VM_DETECTED] = new EventRiskRule { Weight = 10, Threshold = 10 };
        options.Events[EventType.MONITORING_GAP] = new EventRiskRule { Weight = 0 };
        return options;
    }

    public static EventService Service(AppDbContext db, TimeProvider clock) =>
        new(db, Risks(db, clock), clock);

    /// <summary>Sınava özel ayarı olmayan sınavda test ayarlarıyla aynı motoru veren servis (K-49).</summary>
    public static ExamRiskService Risks(AppDbContext db, TimeProvider clock) =>
        new(db, Microsoft.Extensions.Options.Options.Create(Options()), clock);

    public static FakeTimeProvider Clock() => new(Now);

    /// <summary>Sınavı <see cref="Now"/> anında başlamış, sürmekte olan bir oturum.</summary>
    public static async Task<ExamSession> InProgressSessionAsync(AppDbContext db, SessionState state = SessionState.InProgress)
    {
        var session = new ExamSession
        {
            Exam = TestData.Exam(),
            Candidate = TestData.Candidate(),
            State = state,
            StartedAt = Now,
            EndsAt = Now.AddMinutes(30),
        };
        db.ExamSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }
}
