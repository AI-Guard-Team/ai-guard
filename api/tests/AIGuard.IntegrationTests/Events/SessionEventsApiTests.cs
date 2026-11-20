using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.Events;

/// <summary>HTTP katmanına özgü davranışlar: JSON biçimi, gövde sınırı, yetki ve oturum sahipliği.</summary>
public class SessionEventsApiTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private TestApi? _api;
    private TestApi Api => _api ??= new TestApi(fx);

    // Son oluşturulan oturumun sahibi olan aday adına istemci (E2: yalnızca kendi oturumu).
    private Domain.Entities.User? _owner;
    private HttpClient Client() => Api.As(_owner ?? throw new InvalidOperationException("Önce EventsUrlAsync çağrılmalı."));

    private async Task<string> EventsUrlAsync()
    {
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db);
        _owner = session.Candidate;
        return $"/api/sessions/{session.Id}/events";
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

        var url = await EventsUrlAsync();
        var response = await Client().PostAsync(url, Json(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Gövde boyutu sınırı ([RequestSizeLimit]) TestServer'da uygulanmıyor, yalnızca Kestrel'de;
    // bu yüzden burada test edilmiyor. Kestrel'de 5 KB'lık gövdenin 413 aldığı elle doğrulandı.

    [SkippableFact]
    public async Task Unknown_session_returns_404()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

        await EventsUrlAsync(); // istemci için kayıtlı bir aday

        var response = await Client().PostAsJsonAsync($"/api/sessions/{Guid.NewGuid()}/events", new { type = "TAB_BLUR" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Anonymous_request_is_rejected_with_401()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

        var response = await Api.Anonymous().PostAsJsonAsync(await EventsUrlAsync(), new { type = "TAB_BLUR" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Candidate_cannot_write_to_another_candidates_session()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var otherUrl = await EventsUrlAsync();
        await EventsUrlAsync(); // _owner artık başka bir aday

        var response = await Client().PostAsJsonAsync(otherUrl, new { type = "TAB_BLUR" });

        // 403 değil 404: başka oturumun var olduğu bilgisi verilmiyor.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Non_candidate_roles_get_403()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var url = await EventsUrlAsync();
        var admin = TestData.UserWithRole(Domain.Enums.UserRole.Admin);
        await using (var db = fx.CreateContext())
        {
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }

        var response = await Api.As(admin).PostAsJsonAsync(url, new { type = "TAB_BLUR" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
