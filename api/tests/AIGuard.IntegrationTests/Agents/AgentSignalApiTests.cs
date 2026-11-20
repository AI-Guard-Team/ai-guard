using System.Net;
using System.Net.Http.Json;
using AIGuard.Api.Controllers;
using AIGuard.Application.Agents;
using AIGuard.Domain.Enums;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.Agents;

/// <summary>Sinyal ucunun HTTP davranışı: JWT değil HMAC ile korunuyor (K-20, K-21).</summary>
public class AgentSignalApiTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private TestApi? _api;
    private TestApi Api => _api ??= new TestApi(fx);

    /// <summary>Gerçek akış: aday kod alır, ajan girişsiz eşleşir ve anahtarı yanıttan alır.</summary>
    private async Task<(Guid SessionId, byte[] Key)> PairAsync()
    {
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var issue = await Api.As(session.Candidate).PostAsync($"/api/sessions/{session.Id}/agent-code", null);
        var code = (await issue.Content.ReadFromJsonAsync<PairingCodeResponse>())!.Code;
        var pair = await Api.Anonymous().PostAsJsonAsync("/api/agent/pair", new { code, os = "Windows" });
        var body = (await pair.Content.ReadFromJsonAsync<AgentPairResponse>())!;
        Assert.Equal("no-store", pair.Headers.CacheControl?.ToString());
        return (session.Id, Convert.FromBase64String(body.SessionKey));
    }

    private static object Signal(Guid sessionId, byte[] key, string[]? indicators = null)
    {
        indicators ??= [];
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = PairedAgent.NewNonce();
        return new { sessionId, ts, nonce, indicators, sig = AgentSignature.Sign(key, sessionId, ts, nonce, indicators) };
    }

    [SkippableFact]
    public async Task Agent_signs_with_the_key_from_pairing_and_signal_is_accepted_without_jwt()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (sessionId, key) = await PairAsync();

        var response = await Api.Anonymous().PostAsJsonAsync("/api/agent/signal", Signal(sessionId, key));

        Assert.Equal(32, key.Length);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [SkippableFact]
    public async Task Wrong_signature_returns_401_without_the_reason()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (sessionId, _) = await PairAsync();

        var response = await Api.Anonymous().PostAsJsonAsync("/api/agent/signal", Signal(sessionId, AgentSignature.GenerateKey()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("signature", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Unknown_session_returns_404()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

        var response = await Api.Anonymous().PostAsJsonAsync("/api/agent/signal", Signal(Guid.NewGuid(), AgentSignature.GenerateKey()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Ended_session_returns_410_so_the_agent_stops()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (sessionId, key) = await PairAsync();
        await using (var db = fx.CreateContext())
        {
            (await db.ExamSessions.FindAsync(sessionId))!.State = SessionState.Completed;
            await db.SaveChangesAsync();
        }

        var response = await Api.Anonymous().PostAsJsonAsync("/api/agent/signal", Signal(sessionId, key));

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }
}
