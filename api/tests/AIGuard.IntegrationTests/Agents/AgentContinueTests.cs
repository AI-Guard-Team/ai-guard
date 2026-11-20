using System.Net;
using System.Net.Http.Json;
using AIGuard.Api.Controllers;
using AIGuard.Application.Agents;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Persistence;
using AIGuard.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.Agents;

/// <summary>Kabul kriteri (Y4): ajansız ve göstergeli devam denetim kaydında görünür; gerekçe saklanır,
/// oturum kimlik doğrulamaya geçer (FR-12, FR-20, FR-35).</summary>
public class AgentContinueServiceTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private void RequireDb() => Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

    private static AgentContinueService Service(AppDbContext db, PairedAgent agent) => new(db, agent.Keys, agent.Clock);

    private static async Task<Guid> CandidateOfAsync(AppDbContext db, Guid sessionId) =>
        (await db.ExamSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId)).CandidateId;

    private async Task<(ExamSession Session, List<AuditLog> Audit)> ReloadAsync(Guid sessionId)
    {
        await using var db = fx.CreateContext();
        var session = await db.ExamSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        var audit = await db.AuditLogs.AsNoTracking().Where(a => a.TargetId == sessionId).ToListAsync();
        return (session, audit);
    }

    [SkippableFact]
    public async Task Without_an_agent_candidate_continues_with_a_reason_and_it_is_audited()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var clock = Clock();
        var service = new AgentContinueService(db, new InMemoryAgentKeyStore(), clock);

        var status = await service.GetStatusAsync(session.Id);
        var result = await service.ContinueAsync(session.Id, session.CandidateId,
            new AgentContinueRequest("noAgent", "  Ajan bu bilgisayarda açılmıyor.  "));

        Assert.True(status!.CanContinueWithoutAgent);
        Assert.False(status.CanContinueIndicated);
        Assert.Equal(AgentContinueStatus.Continued, result.Status);
        var (after, audit) = await ReloadAsync(session.Id);
        Assert.Equal(SessionState.IdentityVerification, after.State);
        Assert.True(after.Flags.HasFlag(SessionFlags.NoAgent));
        Assert.Equal("Ajan bu bilgisayarda açılmıyor.", after.ContinueJustification);
        var log = Assert.Single(audit);
        Assert.Equal(("session.continue_no_agent", nameof(ExamSession), session.CandidateId),
            (log.Action, log.TargetType, log.ActorUserId!.Value));
        Assert.Contains("noAgent", log.NewValue);
        Assert.Contains("Ajan bu bilgisayarda açılmıyor.", log.NewValue);
    }

    [SkippableFact]
    public async Task No_agent_is_refused_while_the_agent_is_running()
    {
        // Ajan çalışırken "ajansız" seçilip cihaz denetimi kapatılamamalı.
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);
        await agent.Signals(db).ProcessAsync(agent.Signal([PairedAgent.App("AnyDesk")]));

        var result = await Service(db, agent).ContinueAsync(agent.SessionId, await CandidateOfAsync(db, agent.SessionId),
            new AgentContinueRequest("noAgent", "gerekçe"));

        Assert.Equal(AgentContinueStatus.AgentActive, result.Status);
        var (after, audit) = await ReloadAsync(agent.SessionId);
        Assert.Equal(SessionState.AgentPairing, after.State);
        Assert.Empty(audit);
    }

    [SkippableFact]
    public async Task Right_after_pairing_the_agent_counts_as_running_before_its_first_signal()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);

        var status = await Service(db, agent).GetStatusAsync(agent.SessionId);

        Assert.True(status!.AgentActive);
        Assert.False(status.CanContinueWithoutAgent);
    }

    [SkippableFact]
    public async Task Agent_that_went_silent_allows_no_agent_and_its_key_is_revoked()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);
        agent.Clock.Advance(TimeSpan.FromSeconds(31));

        var result = await Service(db, agent).ContinueAsync(agent.SessionId, await CandidateOfAsync(db, agent.SessionId),
            new AgentContinueRequest("noAgent", "Ajan kapandı, yeniden açılmıyor."));
        agent.Clock.Advance(TimeSpan.FromSeconds(60));
        await agent.Monitor(db).RunOnceAsync();
        var late = await agent.Signals(db).ProcessAsync(agent.Signal([PairedAgent.Vm()]));

        Assert.Equal(AgentContinueStatus.Continued, result.Status);
        Assert.Null(agent.Keys.Get(agent.SessionId));
        Assert.Equal(AgentSignalStatus.KeyUnavailable, late.Status);
        await using var check = fx.CreateContext();
        Assert.Null((await check.AgentPairings.SingleAsync(p => p.ExamSessionId == agent.SessionId)).SessionKeyHash);
        // FR-12: ajansız oturumda cihaz denetimi yok; "sinyal kesildi" de yazılmıyor.
        Assert.False(await check.Events.AnyAsync(e => e.ExamSessionId == agent.SessionId));
    }

    [SkippableFact]
    public async Task Indicators_are_shown_with_reason_and_fix_and_candidate_continues_indicated()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);
        await agent.Signals(db).ProcessAsync(agent.Signal(
            [PairedAgent.App("AnyDesk"), new AgentIndicator("VPN_PROXY", "WireGuard", "wintun"), PairedAgent.Hypervisor()]));
        var service = Service(db, agent);

        var status = await service.GetStatusAsync(agent.SessionId);
        var result = await service.ContinueAsync(agent.SessionId, await CandidateOfAsync(db, agent.SessionId),
            new AgentContinueRequest("indicated", "AnyDesk iş bilgisayarımda zorunlu, kapatamıyorum."));

        Assert.True(status!.CanContinueIndicated);
        Assert.Equal(["AnyDesk", "WireGuard"], status.Indicators.Select(i => i.Name)); // hipervizör gösterilmiyor
        Assert.Contains("AnyDesk", status.Indicators[0].Message);
        Assert.Contains("kapatın", status.Indicators[0].Fix);
        Assert.Contains("VPN", status.Indicators[1].Message);

        Assert.Equal(AgentContinueStatus.Continued, result.Status);
        var (after, audit) = await ReloadAsync(agent.SessionId);
        Assert.Equal(SessionState.IdentityVerification, after.State);
        Assert.True(after.Flags.HasFlag(SessionFlags.Indicated));
        Assert.False(after.Flags.HasFlag(SessionFlags.NoAgent));
        var log = Assert.Single(audit);
        Assert.Equal("session.continue_indicated", log.Action);
        Assert.Contains("WireGuard", log.NewValue);
        Assert.NotNull(agent.Keys.Get(agent.SessionId)); // göstergeli devamda denetim sürüyor
    }

    [SkippableFact]
    public async Task Indicated_needs_an_indicator()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var agent = await PairedAgent.CreateAsync(db);

        var result = await Service(db, agent).ContinueAsync(agent.SessionId, await CandidateOfAsync(db, agent.SessionId),
            new AgentContinueRequest("indicated", "gerekçe"));

        Assert.Equal(AgentContinueStatus.NoIndicators, result.Status);
    }

    [SkippableTheory]
    [InlineData("ajansiz", "gerekçe")]
    [InlineData(null, "gerekçe")]
    [InlineData("noAgent", "   ")]
    [InlineData("noAgent", null)]
    public async Task Unknown_mode_or_empty_reason_is_invalid(string? mode, string? reason)
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var service = new AgentContinueService(db, new InMemoryAgentKeyStore(), Clock());

        var result = await service.ContinueAsync(session.Id, session.CandidateId, new AgentContinueRequest(mode, reason));

        Assert.Equal(AgentContinueStatus.Invalid, result.Status);
    }

    [SkippableFact]
    public async Task Too_long_reason_is_invalid()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var service = new AgentContinueService(db, new InMemoryAgentKeyStore(), Clock());

        var result = await service.ContinueAsync(session.Id, session.CandidateId,
            new AgentContinueRequest("noAgent", new string('a', AgentContinueService.MaxReasonLength + 1)));

        Assert.Equal(AgentContinueStatus.Invalid, result.Status);
    }

    [SkippableTheory]
    [InlineData(SessionState.ConsentPending)]
    [InlineData(SessionState.IdentityVerification)]
    [InlineData(SessionState.InProgress)]
    public async Task Continue_is_only_possible_in_the_pairing_step(SessionState state)
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, state);
        var service = new AgentContinueService(db, new InMemoryAgentKeyStore(), Clock());

        var result = await service.ContinueAsync(session.Id, session.CandidateId, new AgentContinueRequest("noAgent", "gerekçe"));

        Assert.Equal(AgentContinueStatus.WrongState, result.Status);
        Assert.Empty((await ReloadAsync(session.Id)).Audit);
    }
}

/// <summary>Uçların HTTP davranışı: yalnızca aday ve yalnızca kendi oturumu.</summary>
public class SessionAgentApiTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    // API enum'ları adlarıyla yazıyor (K-05).
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private TestApi? _api;
    private TestApi Api => _api ??= new TestApi(fx);

    [SkippableFact]
    public async Task Candidate_sees_status_and_continues_without_agent_over_http()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var client = Api.As(session.Candidate);

        var status = await client.GetFromJsonAsync<AgentStatusView>($"/api/sessions/{session.Id}/agent", Json);
        var response = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/agent/continue",
            new { mode = "noAgent", reason = "Ajan kurulamadı." });

        Assert.True(status!.CanContinueWithoutAgent);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SessionState.IdentityVerification, (await response.Content.ReadFromJsonAsync<AgentContinueResponse>(Json))!.State);
        await using var check = fx.CreateContext();
        Assert.True(await check.AuditLogs.AnyAsync(a => a.TargetId == session.Id && a.Action == AgentContinueService.AuditNoAgent));
    }

    [SkippableFact]
    public async Task Missing_reason_is_400_and_second_continue_is_409()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var client = Api.As(session.Candidate);
        var url = $"/api/sessions/{session.Id}/agent/continue";

        var empty = await client.PostAsJsonAsync(url, new { mode = "noAgent", reason = "" });
        await client.PostAsJsonAsync(url, new { mode = "noAgent", reason = "Ajan kurulamadı." });
        var again = await client.PostAsJsonAsync(url, new { mode = "noAgent", reason = "Ajan kurulamadı." });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Contains("gerekçe", await empty.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [SkippableFact]
    public async Task Other_candidate_and_anonymous_cannot_use_the_endpoints()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var other = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var body = new { mode = "noAgent", reason = "başkasının oturumu" };

        var get = await Api.As(other.Candidate).GetAsync($"/api/sessions/{session.Id}/agent");
        var post = await Api.As(other.Candidate).PostAsJsonAsync($"/api/sessions/{session.Id}/agent/continue", body);
        var anonymous = await Api.Anonymous().PostAsJsonAsync($"/api/sessions/{session.Id}/agent/continue", body);

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(SessionState.AgentPairing, (await fx.CreateContext().ExamSessions.AsNoTracking().SingleAsync(s => s.Id == session.Id)).State);
    }
}
