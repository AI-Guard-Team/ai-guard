using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIGuard.Application.Risk;
using AIGuard.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AIGuard.IntegrationTests.Exams;

/// <summary>UI1 (Stitch 29, K-40): risk ayarları salt okunur, yalnızca yöneticiye; değerler sunucunun kullandığı değerler.</summary>
public class RiskSettingsApiTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [SkippableFact]
    public async Task Admin_reads_the_configured_weights_and_others_are_forbidden()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var api = new TestApi(fx);
        var admin = TestData.UserWithRole(UserRole.Admin);
        var reviewer = TestData.UserWithRole(UserRole.Reviewer);
        await using (var db = fx.CreateContext())
        {
            db.Users.AddRange(admin, reviewer);
            await db.SaveChangesAsync();
        }

        var settings = await api.As(admin).GetFromJsonAsync<RiskSettingsView>("/api/risk-settings", Json);
        var asReviewer = await api.As(reviewer).GetAsync("/api/risk-settings");
        var configured = api.Factory.Services.GetRequiredService<IOptions<RiskOptions>>().Value;

        Assert.Equal(configured.SessionThreshold, settings!.SessionThreshold);
        Assert.Equal(Enum.GetValues<EventType>().Length, settings.Events.Count);
        Assert.Equal(configured.Events[EventType.PHONE].Weight, settings.Events.Single(e => e.Type == EventType.PHONE).Weight);
        Assert.True(settings.Events.Zip(settings.Events.Skip(1)).All(p => p.First.Weight >= p.Second.Weight)); // ağırlığa göre sıralı
        Assert.Equal(HttpStatusCode.Forbidden, asReviewer.StatusCode);
    }
}
