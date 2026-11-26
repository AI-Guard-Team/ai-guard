using AIGuard.Application.Agents;
using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.IntegrationTests.Agents;

/// <summary>Kabul kriteri: bozuk, geciken veya tekrar gönderilen sinyal reddedilir ve olay olarak kaydedilir;
/// bitmiş oturumun anahtarıyla gelen sinyal kabul edilmez (Y2).</summary>
public class AgentSignalServiceTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private void RequireDb() => Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

    private async Task<(int Events, int Valid, int Invalid)> CountsAsync(Guid sessionId)
    {
        await using var db = fx.CreateContext();
        var events = await db.Events.CountAsync(e => e.ExamSessionId == sessionId && e.Type == EventType.AGENT_SIGNAL_INVALID);
        var signals = db.AgentSignals.Where(s => s.AgentPairing.ExamSessionId == sessionId);
        return (events, await signals.CountAsync(s => s.SignatureValid), await signals.CountAsync(s => !s.SignatureValid));
    }

    [SkippableFact]
    public async Task Pairing_gives_the_agent_a_key_and_stores_only_its_hash()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);

        var pairing = await db.AgentPairings.AsNoTracking().SingleAsync(p => p.ExamSessionId == agent.SessionId);
        Assert.Equal(32, agent.Key.Length);
        Assert.Equal(AgentSignature.HashKey(agent.Key), pairing.SessionKeyHash);
        Assert.Equal(agent.Key, agent.Keys.Get(agent.SessionId));
        // Anahtarın kendisi veritabanında yok: yalnızca özet sütunu var, şifreli anahtar sütunu kaldırıldı (S-16).
        var columns = await db.Database
            .SqlQuery<string>($"SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name = 'agent_pairings'")
            .ToListAsync();
        Assert.Contains("session_key_hash", columns);
        Assert.DoesNotContain("encrypted_session_key", columns);
    }

    [SkippableFact]
    public async Task First_valid_signal_without_indicators_is_stored_and_moves_to_identity_verification()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);
        agent.Clock.Advance(TimeSpan.FromSeconds(3));

        var result = await agent.Signals(db).ProcessAsync(agent.Signal());

        Assert.Equal(AgentSignalStatus.Accepted, result.Status);
        await using var check = fx.CreateContext();
        var pairing = await check.AgentPairings.Include(p => p.ExamSession).AsNoTracking().SingleAsync(p => p.ExamSessionId == agent.SessionId);
        Assert.Equal(agent.Clock.GetUtcNow(), pairing.LastSignalAt);
        Assert.Equal(SessionState.IdentityVerification, pairing.ExamSession.State); // UC-08 adım 5
        Assert.Equal((0, 1, 0), await CountsAsync(agent.SessionId));
    }

    [SkippableFact]
    public async Task Signal_with_indicators_is_stored_but_does_not_move_the_session()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);

        var result = await agent.Signals(db).ProcessAsync(agent.Signal([PairedAgent.Vm()]));

        Assert.Equal(AgentSignalStatus.Accepted, result.Status);
        await using var check = fx.CreateContext();
        Assert.Equal(SessionState.AgentPairing, (await check.ExamSessions.FindAsync(agent.SessionId))!.State);
        var stored = await check.AgentSignals.SingleAsync(s => s.AgentPairing.ExamSessionId == agent.SessionId);
        Assert.Equal([PairedAgent.Vm()], AgentIndicator.ParseStored(stored.Indicators));
    }

    [SkippableFact]
    public async Task Tampered_signal_is_rejected_stored_as_invalid_and_recorded_as_event()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);
        // İmza göstergesiz sinyal için atılmış, gövdede gösterge var (aday göstergeyi gizlemeye ya da eklemeye çalışıyor).
        var signal = agent.Signal() with { Indicators = [PairedAgent.Vm()] };

        var result = await agent.Signals(db).ProcessAsync(signal);

        Assert.Equal((AgentSignalStatus.Rejected, AgentSignalRejection.Signature), (result.Status, result.Rejection));
        Assert.Equal((1, 0, 1), await CountsAsync(agent.SessionId));
        await using var check = fx.CreateContext();
        var ev = await check.Events.SingleAsync(e => e.ExamSessionId == agent.SessionId);
        Assert.Contains("signature", ev.Payload);
        Assert.Null((await check.AgentPairings.SingleAsync(p => p.ExamSessionId == agent.SessionId)).LastSignalAt);
    }

    [SkippableFact]
    public async Task Signal_signed_with_a_forged_key_is_rejected()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);

        var result = await agent.Signals(db).ProcessAsync(agent.Signal(key: AgentSignature.GenerateKey()));

        Assert.Equal(AgentSignalRejection.Signature, result.Rejection);
    }

    [SkippableTheory]
    [InlineData(-31)]
    [InlineData(31)]
    public async Task Late_or_future_signal_is_rejected_and_recorded(int offsetSeconds)
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);
        var ts = agent.Clock.GetUtcNow().AddSeconds(offsetSeconds).ToUnixTimeSeconds();

        var result = await agent.Signals(db).ProcessAsync(agent.Signal(ts: ts));

        Assert.Equal((AgentSignalStatus.Rejected, AgentSignalRejection.Stale), (result.Status, result.Rejection));
        Assert.Equal((1, 0, 1), await CountsAsync(agent.SessionId));
    }

    [SkippableTheory]
    [InlineData(-30)]
    [InlineData(30)]
    public async Task Signal_within_thirty_seconds_is_accepted(int offsetSeconds)
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);
        var ts = agent.Clock.GetUtcNow().AddSeconds(offsetSeconds).ToUnixTimeSeconds();

        Assert.Equal(AgentSignalStatus.Accepted, (await agent.Signals(db).ProcessAsync(agent.Signal(ts: ts))).Status);
    }

    [SkippableFact]
    public async Task Replayed_signal_is_rejected_and_recorded_without_a_second_row()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);
        var signal = agent.Signal();

        var first = await agent.Signals(db).ProcessAsync(signal);
        agent.Clock.Advance(TimeSpan.FromSeconds(2)); // pencere içinde; yalnızca nonce tekrarı yakalamalı
        var replay = await agent.Signals(db).ProcessAsync(signal);

        Assert.Equal(AgentSignalStatus.Accepted, first.Status);
        Assert.Equal((AgentSignalStatus.Rejected, AgentSignalRejection.Replay), (replay.Status, replay.Rejection));
        Assert.Equal((1, 1, 0), await CountsAsync(agent.SessionId)); // nonce benzersiz indeksi ikinci satırı engelliyor
    }

    [SkippableFact]
    public async Task Concurrent_replays_are_accepted_only_once()
    {
        RequireDb();
        PairedAgent agent;
        await using (var db = fx.CreateContext())
        {
            agent = await PairedAgent.CreateAsync(db);
        }

        var signal = agent.Signal();
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ =>
        {
            await using var db = fx.CreateContext();
            return await agent.Signals(db).ProcessAsync(signal);
        }));

        Assert.Single(results, r => r.Status == AgentSignalStatus.Accepted);
        Assert.Equal(2, results.Count(r => r.Rejection == AgentSignalRejection.Replay));
    }

    [SkippableTheory]
    [InlineData(SessionState.Completed)]
    [InlineData(SessionState.Cancelled)]
    public async Task Ended_session_key_is_revoked_and_its_signals_are_not_accepted(SessionState ended)
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db, ended);

        var result = await agent.Signals(db).ProcessAsync(agent.Signal());

        Assert.Equal(AgentSignalStatus.SessionEnded, result.Status);
        Assert.Null(agent.Keys.Get(agent.SessionId));
        await using var check = fx.CreateContext();
        Assert.Null((await check.AgentPairings.SingleAsync(p => p.ExamSessionId == agent.SessionId)).SessionKeyHash);
        Assert.Equal((0, 0, 0), await CountsAsync(agent.SessionId));
    }

    [SkippableFact]
    public async Task After_api_restart_signals_cannot_be_verified_and_no_event_is_written()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);
        // Yeniden başlayan API'nin belleği boş.
        var restarted = new AgentSignalService(db, new InMemoryAgentKeyStore(), Events.EventTestHelpers.Service(db, agent.Clock), agent.Clock);

        var result = await restarted.ProcessAsync(agent.Signal());

        Assert.Equal(AgentSignalStatus.KeyUnavailable, result.Status);
        Assert.Equal((0, 0, 0), await CountsAsync(agent.SessionId));
    }

    [SkippableFact]
    public async Task Old_key_stops_working_after_pairing_again()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);
        var pairing = new AgentPairingService(db, agent.Clock, agent.Keys);
        await pairing.PairAsync((await pairing.IssueCodeAsync(agent.SessionId)).Code, AgentPlatform.Windows);

        var result = await agent.Signals(db).ProcessAsync(agent.Signal());

        Assert.Equal(AgentSignalRejection.Signature, result.Rejection);
    }

    [SkippableTheory]
    [InlineData("nonce")]
    [InlineData("sig")]
    [InlineData("indicators")]
    [InlineData("type")]
    [InlineData("name")]
    [InlineData("detail")]
    [InlineData("ts")]
    public async Task Malformed_signals_are_refused_without_storing_anything(string field)
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);
        var s = agent.Signal();
        s = field switch
        {
            "nonce" => s with { Nonce = "kısa" },
            "sig" => s with { Sig = null },
            "indicators" => s with { Indicators = Enumerable.Repeat(PairedAgent.App(), AgentSignalService.MaxIndicators + 1).ToArray() },
            "type" => s with { Indicators = [new AgentIndicator("ANYDESK", "AnyDesk")] },
            "name" => s with { Indicators = [new AgentIndicator("FORBIDDEN_APP", "")] },
            "detail" => s with { Indicators = [new AgentIndicator("FORBIDDEN_APP", "AnyDesk", new string('x', AgentIndicator.MaxDetailLength + 1))] },
            _ => s with { Ts = -1 },
        };

        Assert.Equal(AgentSignalStatus.Malformed, (await agent.Signals(db).ProcessAsync(s)).Status);
        Assert.Equal((0, 0, 0), await CountsAsync(agent.SessionId));
    }

    [SkippableFact]
    public async Task Unpaired_session_is_not_found()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);

        var result = await agent.Signals(db).ProcessAsync(agent.Signal() with { SessionId = Guid.NewGuid() });

        Assert.Equal(AgentSignalStatus.NotFound, result.Status);
    }
}
