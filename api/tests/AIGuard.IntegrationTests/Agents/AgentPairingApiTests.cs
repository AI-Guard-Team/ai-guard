using System.Net;
using System.Net.Http.Json;
using AIGuard.Api.Controllers;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.Agents;

/// <summary>Eşleştirme uçlarının HTTP davranışı.</summary>
public class AgentPairingApiTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private TestApi? _api;
    private TestApi Api => _api ??= new TestApi(fx);

    private async Task<(Guid SessionId, User Owner)> SessionAsync(SessionState state = SessionState.AgentPairing)
    {
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, state);
        return (session.Id, session.Candidate);
    }

    private static async Task<string> CodeAsync(HttpClient client, Guid sessionId)
    {
        var issue = await client.PostAsync($"/api/sessions/{sessionId}/agent-code", null);
        return (await issue.Content.ReadFromJsonAsync<PairingCodeResponse>())!.Code;
    }

    [SkippableFact]
    public async Task Browser_gets_a_code_and_agent_pairs_with_it()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (sessionId, owner) = await SessionAsync();

        var issue = await Api.As(owner).PostAsync($"/api/sessions/{sessionId}/agent-code", null);
        var code = await issue.Content.ReadFromJsonAsync<PairingCodeResponse>();
        // Ajanın JWT'si yok; uç eşleştirme koduyla korunuyor.
        var pair = await Api.Anonymous().PostAsJsonAsync("/api/agent/pair", new { code = code!.Code, os = "Windows" });
        var body = await pair.Content.ReadFromJsonAsync<AgentPairResponse>();

        Assert.Equal(HttpStatusCode.Created, issue.StatusCode);
        Assert.Equal("no-store", issue.Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.OK, pair.StatusCode);
        Assert.Equal(sessionId, body!.SessionId);
    }

    [SkippableTheory]
    [InlineData("""{"code":"CODE"}""")]                  // işletim sistemi yok
    [InlineData("""{"code":"CODE","os":"Linux"}""")]     // tanımsız işletim sistemi
    [InlineData("""{"code":"CODE","os":0}""")]           // sayı kabul edilmiyor (K-05)
    [InlineData("""{"code":"yanlis","os":"Windows"}""")] // geçersiz kod
    public async Task Invalid_pair_requests_return_400_without_revealing_the_reason(string json)
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (sessionId, owner) = await SessionAsync();
        var code = await CodeAsync(Api.As(owner), sessionId);

        var response = await Api.Anonymous().PostAsync("/api/agent/pair",
            new StringContent(json.Replace("CODE", code), System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Code_is_not_issued_after_the_pairing_step()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (sessionId, owner) = await SessionAsync(SessionState.InProgress);

        var response = await Api.As(owner).PostAsync($"/api/sessions/{sessionId}/agent-code", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [SkippableFact]
    public async Task Code_requires_login()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (sessionId, _) = await SessionAsync();

        var response = await Api.Anonymous().PostAsync($"/api/sessions/{sessionId}/agent-code", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Candidate_cannot_get_a_code_for_another_candidates_session()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (sessionId, _) = await SessionAsync();
        var (_, other) = await SessionAsync();

        var response = await Api.As(other).PostAsync($"/api/sessions/{sessionId}/agent-code", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
