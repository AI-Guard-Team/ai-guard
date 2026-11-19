using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.Events;

/// <summary>HTTP katmanına özgü davranışlar: JSON biçimi, gövde sınırı, ortam kısıtı.</summary>
public class SessionEventsApiTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private HttpClient Client(string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment(environment);
            b.UseSetting("ConnectionStrings:Default", fx.ConnectionString);
        }).CreateClient();

    private async Task<string> EventsUrlAsync()
    {
        await using var db = fx.CreateContext();
        return $"/api/sessions/{(await InProgressSessionAsync(db)).Id}/events";
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    [SkippableFact]
    public async Task Accepted_event_ignores_client_source_and_returns_no_score()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var url = await EventsUrlAsync();

        var response = await Client().PostAsJsonAsync(url, new { type = "COPY_PASTE", source = "camera", payload = new { text = "gizli" } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain("score", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        await using var db = fx.CreateContext();
        var stored = await db.Events.AsNoTracking().SingleAsync(e => url.Contains(e.ExamSessionId.ToString()));
        Assert.Equal(Domain.Enums.EventSource.Browser, stored.Source);
        Assert.Null(stored.Payload); // tarayıcıdan serbest veri kaydedilmiyor
    }

    [SkippableTheory]
    [InlineData("{\"type\": 7}")]
    [InlineData("{\"type\": \"NOT_AN_EVENT\"}")]
    [InlineData("{\"type\": \"PHONE\"}")]
    public async Task Numeric_unknown_or_server_side_types_are_rejected(string json)
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

        var response = await Client().PostAsync(await EventsUrlAsync(), Json(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Gövde boyutu sınırı ([RequestSizeLimit]) TestServer'da uygulanmıyor, yalnızca Kestrel'de;
    // bu yüzden burada test edilmiyor. Kestrel'de 5 KB'lık gövdenin 413 aldığı elle doğrulandı.

    [SkippableFact]
    public async Task Unknown_session_returns_404()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

        var response = await Client().PostAsJsonAsync($"/api/sessions/{Guid.NewGuid()}/events", new { type = "TAB_BLUR" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Endpoint_is_closed_outside_development_until_authentication_exists()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

        var response = await Client("Production").PostAsJsonAsync(await EventsUrlAsync(), new { type = "TAB_BLUR" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
