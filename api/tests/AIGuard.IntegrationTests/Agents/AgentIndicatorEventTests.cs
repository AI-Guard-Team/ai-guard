using AIGuard.Application.Agents;
using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.IntegrationTests.Agents;

/// <summary>Kabul kriteri (Y3): ajan doğru olayı üretir; hipervizör tek başına oturumu göstergeli yapmaz.
/// Aynı gösterge her sinyalde değil, ilk görüldüğünde olay olur (S-11).</summary>
public class AgentIndicatorEventTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private void RequireDb() => Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

    private async Task<List<(EventType Type, string? Payload)>> EventsAsync(Guid sessionId)
    {
        await using var db = fx.CreateContext();
        return (await db.Events.Where(e => e.ExamSessionId == sessionId).OrderBy(e => e.ReceivedAt)
            .Select(e => new { e.Type, e.Payload }).ToListAsync())
            .Select(e => (e.Type, e.Payload)).ToList();
    }

    private async Task<SessionState> StateAsync(Guid sessionId)
    {
        await using var db = fx.CreateContext();
        return (await db.ExamSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId)).State;
    }

    private static async Task SendAsync(PairedAgent agent, AppDbContext db, params AgentIndicator[] indicators)
    {
        agent.Clock.Advance(TimeSpan.FromSeconds(5));
        var result = await agent.Signals(db).ProcessAsync(agent.Signal(indicators));
        Assert.Equal(AgentSignalStatus.Accepted, result.Status);
    }

    [SkippableFact]
    public async Task Each_indicator_becomes_an_event_of_its_own_type_with_name_and_detail()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);

        await SendAsync(agent, db, PairedAgent.Vm(), PairedAgent.App("AnyDesk"));

        var events = await EventsAsync(agent.SessionId);
        Assert.Equal([EventType.VM_DETECTED, EventType.FORBIDDEN_APP], events.Select(e => e.Type).Order());
        Assert.Contains("\"name\":\"AnyDesk\"", events.Single(e => e.Type == EventType.FORBIDDEN_APP).Payload!.Replace(" ", ""));
        Assert.Contains("\"detail\":\"AnyDesk.exe\"", events.Single(e => e.Type == EventType.FORBIDDEN_APP).Payload!.Replace(" ", ""));
        Assert.Equal(SessionState.AgentPairing, await StateAsync(agent.SessionId)); // gerekçeyle devam Y4'te
    }

    [SkippableFact]
    public async Task Same_indicator_in_every_signal_is_recorded_once()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);

        for (var i = 0; i < 4; i++)
        {
            await SendAsync(agent, db, PairedAgent.App("Zoom"));
        }

        Assert.Single(await EventsAsync(agent.SessionId));
    }

    [SkippableFact]
    public async Task Indicator_that_disappears_and_comes_back_is_recorded_again()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);

        await SendAsync(agent, db, PairedAgent.App("Discord"));
        await SendAsync(agent, db);                                       // aday uygulamayı kapattı
        await SendAsync(agent, db, PairedAgent.App("Discord"));           // yeniden açtı
        await SendAsync(agent, db, PairedAgent.App("Discord"), PairedAgent.App("Zoom"));

        var events = await EventsAsync(agent.SessionId);
        Assert.Equal(3, events.Count);
        Assert.All(events, e => Assert.Equal(EventType.FORBIDDEN_APP, e.Type));
    }

    [SkippableFact]
    public async Task Hypervisor_alone_is_only_an_info_event_and_does_not_hold_the_session()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);

        await SendAsync(agent, db, PairedAgent.Hypervisor());

        Assert.Equal([EventType.HYPERVISOR_INFO], (await EventsAsync(agent.SessionId)).Select(e => e.Type));
        Assert.Equal(SessionState.IdentityVerification, await StateAsync(agent.SessionId)); // UC-08 4b

        await using var check = fx.CreateContext();
        var session = await check.ExamSessions.AsNoTracking().SingleAsync(s => s.Id == agent.SessionId);
        Assert.False(session.Flags.HasFlag(SessionFlags.Risky));
    }

    [SkippableFact]
    public async Task Rejected_signal_produces_no_indicator_event()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);
        var forged = agent.Signal() with { Indicators = [PairedAgent.Vm()] };

        await agent.Signals(db).ProcessAsync(forged);

        Assert.Equal([EventType.AGENT_SIGNAL_INVALID], (await EventsAsync(agent.SessionId)).Select(e => e.Type));
    }
}
