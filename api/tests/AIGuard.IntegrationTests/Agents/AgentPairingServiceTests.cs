using AIGuard.Application.Agents;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.Agents;

public class AgentPairingServiceTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private void RequireDb() => Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

    [SkippableFact]
    public async Task Code_from_the_browser_pairs_the_agent_and_only_its_hash_is_stored()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var clock = Clock();
        var service = new AgentPairingService(db, clock);

        var issued = await service.IssueCodeAsync(session.Id);
        clock.Advance(TimeSpan.FromSeconds(5));
        var paired = await service.PairAsync(issued.Code);

        Assert.Equal(PairAgentStatus.Paired, paired.Status);
        Assert.Equal(session.Id, paired.SessionId);
        await using var check = fx.CreateContext();
        var stored = await check.AgentPairings.AsNoTracking().SingleAsync(p => p.ExamSessionId == session.Id);
        Assert.Equal(paired.PairingId, stored.Id);
        Assert.Equal(Now.AddSeconds(5), stored.PairedAt);
        Assert.Equal(PairingCode.Hash(issued.Code!), stored.PairingCodeHash);
        Assert.NotEqual(issued.Code, stored.PairingCodeHash); // kodun kendisi saklanmıyor
    }

    [SkippableFact]
    public async Task Code_can_be_used_only_once()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var service = new AgentPairingService(db, Clock());
        var issued = await service.IssueCodeAsync(session.Id);

        var first = await service.PairAsync(issued.Code);
        var second = await service.PairAsync(issued.Code);

        Assert.Equal(PairAgentStatus.Paired, first.Status);
        Assert.Equal(PairAgentStatus.Rejected, second.Status);
    }

    [SkippableFact]
    public async Task Concurrent_requests_with_the_same_code_pair_only_once()
    {
        RequireDb();
        Guid sessionId;
        string code;
        await using (var db = fx.CreateContext())
        {
            sessionId = (await InProgressSessionAsync(db, SessionState.AgentPairing)).Id;
            code = (await new AgentPairingService(db, Clock()).IssueCodeAsync(sessionId)).Code!;
        }

        // İki ajan aynı kodu aynı anda gönderiyor; her biri kendi bağlantısıyla.
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var db = fx.CreateContext();
            return await new AgentPairingService(db, Clock()).PairAsync(code);
        }));

        Assert.Single(results, r => r.Status == PairAgentStatus.Paired);
    }

    [SkippableFact]
    public async Task Expired_code_is_rejected()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var clock = Clock();
        var service = new AgentPairingService(db, clock);
        var issued = await service.IssueCodeAsync(session.Id);

        clock.Advance(AgentPairingService.CodeLifetime);

        Assert.Equal(PairAgentStatus.Rejected, (await service.PairAsync(issued.Code)).Status);
    }

    [SkippableFact]
    public async Task New_code_replaces_the_previous_one_and_allows_pairing_again()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var service = new AgentPairingService(db, Clock());

        var old = await service.IssueCodeAsync(session.Id);
        Assert.Equal(PairAgentStatus.Paired, (await service.PairAsync(old.Code)).Status);
        var renewed = await service.IssueCodeAsync(session.Id);

        Assert.Equal(PairAgentStatus.Rejected, (await service.PairAsync(old.Code)).Status);
        Assert.Equal(PairAgentStatus.Paired, (await service.PairAsync(renewed.Code)).Status);
        Assert.Equal(1, await db.AgentPairings.CountAsync(p => p.ExamSessionId == session.Id));
    }

    [SkippableFact]
    public async Task Unknown_or_malformed_codes_are_rejected()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var service = new AgentPairingService(db, Clock());

        Assert.Equal(PairAgentStatus.Rejected, (await service.PairAsync(PairingCode.Generate())).Status);
        Assert.Equal(PairAgentStatus.Rejected, (await service.PairAsync("x")).Status);
        Assert.Equal(PairAgentStatus.Rejected, (await service.PairAsync(null)).Status);
    }

    [SkippableTheory]
    [InlineData(SessionState.Completed)]
    [InlineData(SessionState.Cancelled)]
    [InlineData(SessionState.Decided)]
    public async Task Closed_sessions_get_no_code(SessionState state)
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, state);

        var result = await new AgentPairingService(db, Clock()).IssueCodeAsync(session.Id);

        Assert.Equal(IssuePairingCodeStatus.SessionClosed, result.Status);
        Assert.Null(result.Code);
    }

    [SkippableFact]
    public async Task Code_issued_before_the_session_closed_can_no_longer_pair()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var service = new AgentPairingService(db, Clock());
        var issued = await service.IssueCodeAsync(session.Id);

        session.State = SessionState.Cancelled;
        await db.SaveChangesAsync();

        Assert.Equal(PairAgentStatus.Rejected, (await service.PairAsync(issued.Code)).Status);
    }

    [SkippableFact]
    public async Task Unknown_session_gets_no_code()
    {
        RequireDb();
        await using var db = fx.CreateContext();

        var result = await new AgentPairingService(db, Clock()).IssueCodeAsync(Guid.NewGuid());

        Assert.Equal(IssuePairingCodeStatus.SessionNotFound, result.Status);
    }
}
