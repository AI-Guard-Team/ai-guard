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

    private async Task<Guid> SessionIdAsync()
    {
        await using var db = fx.CreateContext();
        return (await InProgressSessionAsync(db, SessionState.AgentPairing)).Id;
    }

    [SkippableFact]
    public async Task Browser_gets_a_code_and_agent_pairs_with_it()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var client = Client();
        var sessionId = await SessionIdAsync();

        var issue = await client.PostAsync($"/api/sessions/{sessionId}/agent/pairing-code", null);
        var code = await issue.Content.ReadFromJsonAsync<PairingCodeResponse>();
        var pair = await client.PostAsJsonAsync("/api/agent/pair", new { code = code!.Code });
        var body = await pair.Content.ReadFromJsonAsync<AgentPairResponse>();

        Assert.Equal(HttpStatusCode.Created, issue.StatusCode);
        Assert.Equal("no-store", issue.Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.OK, pair.StatusCode);
        Assert.Equal(sessionId, body!.SessionId);
    }

    [SkippableFact]
    public async Task Rejected_pairing_returns_400_without_revealing_the_reason()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

        var response = await Client().PostAsJsonAsync("/api/agent/pair", new { code = "yanlis" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Code_endpoint_is_closed_outside_development_until_authentication_exists()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

        var response = await Client("Production").PostAsync($"/api/sessions/{await SessionIdAsync()}/agent/pairing-code", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
