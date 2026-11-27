using System.Net;
using System.Text.Json;
using AIGuard.Application.Audit;
using AIGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.IntegrationTests.DataProtection;

/// <summary>
/// S-27 (GAB 3.3.2 "Reddedilen istekler ve kritik işlemler denetim kaydına yazılmalı"): her 401 ve 403 kayda giriyor.
/// </summary>
public class AccessDeniedAuditTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private TestApi? _api;
    private TestApi Api => _api ??= new TestApi(fx);

    private async Task<User> CandidateAsync()
    {
        await using var db = fx.CreateContext();
        var user = TestData.Candidate();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<List<AuditLog>> DenialsAsync(Guid userId)
    {
        await using var db = fx.CreateContext();
        return await db.AuditLogs.AsNoTracking()
            .Where(a => a.ActorUserId == userId && a.Action == AccessDeniedRecorder.Action)
            .ToListAsync();
    }

    private static JsonElement Value(AuditLog log) => JsonDocument.Parse(log.NewValue!).RootElement;

    [SkippableFact]
    public async Task Forbidden_request_is_recorded_with_status_route_and_path()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var candidate = await CandidateAsync();

        var response = await Api.As(candidate).GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var log = Assert.Single(await DenialsAsync(candidate.Id));
        Assert.Equal("endpoint", log.TargetType);
        Assert.Equal(403, Value(log).GetProperty("status").GetInt32());
        Assert.Equal("GET", Value(log).GetProperty("method").GetString());
        Assert.Equal("/api/users", Value(log).GetProperty("path").GetString());
        Assert.Equal("api/users", Value(log).GetProperty("route").GetString());
    }

    [SkippableFact]
    public async Task Every_denial_is_recorded()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var candidate = await CandidateAsync();
        var client = Api.As(candidate);

        await client.GetAsync("/api/users");
        await client.GetAsync("/api/users");
        await client.GetAsync("/api/audit-logs");

        Assert.Equal(3, (await DenialsAsync(candidate.Id)).Count);
    }

    [SkippableFact]
    public async Task Unauthenticated_request_is_recorded_without_a_user()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        // Yol benzersiz olsun ki paralel testlerin kayıtlarından ayrılsın.
        var path = $"/api/exams/{Guid.NewGuid()}";

        var response = await Api.Anonymous().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await using var db = fx.CreateContext();
        // NewValue jsonb; yola göre süzme bellekte.
        var logs = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == AccessDeniedRecorder.Action && a.ActorUserId == null)
            .ToListAsync();
        var log = Assert.Single(logs, l => Value(l).GetProperty("path").GetString() == path);
        Assert.Null(log.ActorUserId);
        Assert.Equal(401, Value(log).GetProperty("status").GetInt32());
    }

    [SkippableFact]
    public async Task Allowed_request_is_not_recorded()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var candidate = await CandidateAsync();

        var allowed = await Api.As(candidate).GetAsync("/api/exams");

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Empty(await DenialsAsync(candidate.Id));
    }
}
