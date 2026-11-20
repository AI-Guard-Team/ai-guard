using AIGuard.Infrastructure.Security;
using AIGuard.Application.Agents;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.Agents;

public class AgentPairingServiceTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private const AgentPlatform Windows = AgentPlatform.Windows;

    private void RequireDb() => Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

    [SkippableFact]
    public async Task Code_from_the_browser_pairs_the_agent_and_stores_platform_time_and_only_the_hash()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var clock = Clock();
        var service = new AgentPairingService(db, clock, new InMemoryAgentKeyStore());

        var issued = await service.IssueCodeAsync(session.Id);
        clock.Advance(TimeSpan.FromSeconds(5));
        var paired = await service.PairAsync(issued.Code, AgentPlatform.MacOS);

        Assert.Equal(PairAgentStatus.Paired, paired.Status);
        Assert.Equal(session.Id, paired.SessionId);
        await using var check = fx.CreateContext();
        var stored = await check.AgentPairings.AsNoTracking().SingleAsync(p => p.ExamSessionId == session.Id);
        Assert.Equal(paired.PairingId, stored.Id);
        Assert.Equal(Now.AddSeconds(5), stored.PairedAt);
        Assert.Equal(AgentPlatform.MacOS, stored.Platform);
        Assert.Equal(PairingCode.Hash(issued.Code!), stored.PairingCodeHash);
        Assert.NotEqual(issued.Code, stored.PairingCodeHash); // kodun kendisi saklanmıyor
    }

    [SkippableFact]
    public async Task Code_can_be_used_only_once()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var service = new AgentPairingService(db, Clock(), new InMemoryAgentKeyStore());
        var issued = await service.IssueCodeAsync(session.Id);

        var first = await service.PairAsync(issued.Code, Windows);
        var second = await service.PairAsync(issued.Code, Windows);

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
            code = (await new AgentPairingService(db, Clock(), new InMemoryAgentKeyStore()).IssueCodeAsync(sessionId)).Code!;
        }

        // İki ajan aynı kodu aynı anda gönderiyor; her biri kendi bağlantısıyla.
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var db = fx.CreateContext();
            return await new AgentPairingService(db, Clock(), new InMemoryAgentKeyStore()).PairAsync(code, Windows);
        }));

        Assert.Single(results, r => r.Status == PairAgentStatus.Paired);
    }

    [SkippableFact]
    public async Task Code_is_valid_for_five_minutes()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var clock = Clock();
        var service = new AgentPairingService(db, clock, new InMemoryAgentKeyStore());
        var stillValid = await service.IssueCodeAsync((await InProgressSessionAsync(db, SessionState.AgentPairing)).Id);
        var expired = await service.IssueCodeAsync((await InProgressSessionAsync(db, SessionState.AgentPairing)).Id);

        clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        Assert.Equal(PairAgentStatus.Paired, (await service.PairAsync(stillValid.Code, Windows)).Status);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(PairAgentStatus.Rejected, (await service.PairAsync(expired.Code, Windows)).Status);
    }

    [SkippableFact]
    public async Task New_code_replaces_the_previous_one()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var service = new AgentPairingService(db, Clock(), new InMemoryAgentKeyStore());

        var old = await service.IssueCodeAsync(session.Id);
        var renewed = await service.IssueCodeAsync(session.Id);

        Assert.Equal(PairAgentStatus.Rejected, (await service.PairAsync(old.Code, Windows)).Status);
        Assert.Equal(PairAgentStatus.Paired, (await service.PairAsync(renewed.Code, Windows)).Status);
        Assert.Equal(1, await db.AgentPairings.CountAsync(p => p.ExamSessionId == session.Id));
    }

    [SkippableFact]
    public async Task Unknown_or_malformed_codes_are_rejected()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var service = new AgentPairingService(db, Clock(), new InMemoryAgentKeyStore());

        Assert.Equal(PairAgentStatus.Rejected, (await service.PairAsync(PairingCode.Generate(), Windows)).Status);
        Assert.Equal(PairAgentStatus.Rejected, (await service.PairAsync("x", Windows)).Status);
        Assert.Equal(PairAgentStatus.Rejected, (await service.PairAsync(null, Windows)).Status);
    }

    [SkippableTheory]
    [InlineData(null)]
    [InlineData(99)]
    public async Task Missing_or_unknown_platform_is_rejected_and_code_stays_usable(int? platform)
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var service = new AgentPairingService(db, Clock(), new InMemoryAgentKeyStore());
        var issued = await service.IssueCodeAsync((await InProgressSessionAsync(db, SessionState.AgentPairing)).Id);

        var result = await service.PairAsync(issued.Code, (AgentPlatform?)platform);

        Assert.Equal(PairAgentStatus.Rejected, result.Status);
        Assert.Equal(PairAgentStatus.Paired, (await service.PairAsync(issued.Code, Windows)).Status);
    }

    [SkippableTheory]
    [InlineData(SessionState.ConsentPending)]
    [InlineData(SessionState.IdentityVerification)]
    [InlineData(SessionState.InProgress)]
    [InlineData(SessionState.Completed)]
    [InlineData(SessionState.Cancelled)]
    public async Task Code_is_issued_only_in_agent_pairing_state(SessionState state)
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, state);

        var result = await new AgentPairingService(db, Clock(), new InMemoryAgentKeyStore()).IssueCodeAsync(session.Id);

        Assert.Equal(IssuePairingCodeStatus.NotInPairingState, result.Status);
        Assert.Null(result.Code);
    }

    [SkippableFact]
    public async Task Code_issued_before_the_session_closed_can_no_longer_pair()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var service = new AgentPairingService(db, Clock(), new InMemoryAgentKeyStore());
        var issued = await service.IssueCodeAsync(session.Id);

        session.State = SessionState.Cancelled;
        await db.SaveChangesAsync();

        Assert.Equal(PairAgentStatus.Rejected, (await service.PairAsync(issued.Code, Windows)).Status);
    }

    [SkippableFact]
    public async Task Unknown_session_gets_no_code()
    {
        RequireDb();
        await using var db = fx.CreateContext();

        var result = await new AgentPairingService(db, Clock(), new InMemoryAgentKeyStore()).IssueCodeAsync(Guid.NewGuid());

        Assert.Equal(IssuePairingCodeStatus.SessionNotFound, result.Status);
    }
}
