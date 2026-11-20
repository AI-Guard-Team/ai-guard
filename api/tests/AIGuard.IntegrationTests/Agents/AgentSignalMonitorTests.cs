using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.IntegrationTests.Agents;

/// <summary>30 sn sinyal yoksa AGENT_SIGNAL_LOST; biten oturumların anahtarı geçersiz (Y2).</summary>
public class AgentSignalMonitorTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private void RequireDb() => Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

    private async Task<int> LostEventsAsync(Guid sessionId)
    {
        await using var db = fx.CreateContext();
        return await db.Events.CountAsync(e => e.ExamSessionId == sessionId && e.Type == EventType.AGENT_SIGNAL_LOST);
    }

    [SkippableFact]
    public async Task No_signal_for_thirty_seconds_after_pairing_records_one_lost_event()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);

        agent.Clock.Advance(TimeSpan.FromSeconds(29));
        await agent.Monitor(db).RunOnceAsync();
        Assert.Equal(0, await LostEventsAsync(agent.SessionId));

        agent.Clock.Advance(TimeSpan.FromSeconds(1));
        await agent.Monitor(db).RunOnceAsync();
        agent.Clock.Advance(TimeSpan.FromSeconds(10));
        await agent.Monitor(db).RunOnceAsync(); // aynı kesinti için ikinci olay yok

        Assert.Equal(1, await LostEventsAsync(agent.SessionId));
    }

    [SkippableFact]
    public async Task Each_new_gap_after_a_valid_signal_records_a_new_lost_event()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);

        agent.Clock.Advance(TimeSpan.FromSeconds(31));
        await agent.Monitor(db).RunOnceAsync();
        await agent.Signals(db).ProcessAsync(agent.Signal()); // ajan geri geldi
        agent.Clock.Advance(TimeSpan.FromSeconds(20));
        await agent.Monitor(db).RunOnceAsync();
        Assert.Equal(1, await LostEventsAsync(agent.SessionId));

        agent.Clock.Advance(TimeSpan.FromSeconds(10));
        await agent.Monitor(db).RunOnceAsync();

        Assert.Equal(2, await LostEventsAsync(agent.SessionId));
    }

    [SkippableFact]
    public async Task Regular_signals_never_produce_a_lost_event()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);

        for (var i = 0; i < 12; i++)
        {
            agent.Clock.Advance(TimeSpan.FromSeconds(5));
            await agent.Signals(db).ProcessAsync(agent.Signal());
            await agent.Monitor(db).RunOnceAsync();
        }

        Assert.Equal(0, await LostEventsAsync(agent.SessionId));
    }

    [SkippableTheory]
    [InlineData(SessionState.Completed)]
    [InlineData(SessionState.Cancelled)]
    public async Task Ended_session_gets_its_key_revoked_and_no_lost_event(SessionState ended)
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db, ended);

        agent.Clock.Advance(TimeSpan.FromMinutes(1));
        await agent.Monitor(db).RunOnceAsync();

        Assert.Null(agent.Keys.Get(agent.SessionId));
        await using var check = fx.CreateContext();
        Assert.Null((await check.AgentPairings.SingleAsync(p => p.ExamSessionId == agent.SessionId)).SessionKeyHash);
        Assert.Equal(0, await LostEventsAsync(agent.SessionId));
    }
}
