using System.Net;
using System.Net.Http.Json;
using AIGuard.Api.Controllers;
using AIGuard.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.Agents;

/// <summary>Eşleştirme uçlarının HTTP davranışı.</summary>
public class AgentPairingApiTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private HttpClient Client(string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment(environment);
            b.UseSetting("ConnectionStrings:Default", fx.ConnectionString);
        }).CreateClient();

    private async Task<Guid> SessionIdAsync(SessionState state = SessionState.AgentPairing)
    {
        await using var db = fx.CreateContext();
        return (await InProgressSessionAsync(db, state)).Id;
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
        var client = Client();
        var sessionId = await SessionIdAsync();

        var issue = await client.PostAsync($"/api/sessions/{sessionId}/agent-code", null);
        var code = await issue.Content.ReadFromJsonAsync<PairingCodeResponse>();
        var pair = await client.PostAsJsonAsync("/api/agent/pair", new { code = code!.Code, os = "Windows" });
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
        var client = Client();
        var code = await CodeAsync(client, await SessionIdAsync());

        var response = await client.PostAsync("/api/agent/pair",
            new StringContent(json.Replace("CODE", code), System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Code_is_not_issued_after_the_pairing_step()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

        var response = await Client().PostAsync($"/api/sessions/{await SessionIdAsync(SessionState.InProgress)}/agent-code", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [SkippableFact]
    public async Task Code_endpoint_is_closed_outside_development_until_authentication_exists()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

        var response = await Client("Production").PostAsync($"/api/sessions/{await SessionIdAsync()}/agent-code", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
