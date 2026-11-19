using System.Net;
using AIGuard.Application.Events;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.Events;

public class SessionIpGuardTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private static readonly IPAddress First = IPAddress.Parse("88.230.10.20");
    private static readonly IPAddress Second = IPAddress.Parse("78.180.5.6");

    [SkippableFact]
    public async Task First_request_stores_ip_and_change_is_reported_once()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db);
        var clock = Clock();
        var guard = new SessionIpGuard(db, Service(db, clock), clock);

        await guard.CheckAsync(session.Id, First);
        await guard.CheckAsync(session.Id, First);
        await guard.CheckAsync(session.Id, Second);
        await guard.CheckAsync(session.Id, Second); // aynı yeni IP için tekrar olay üretilmemeli

        await db.Entry(session).ReloadAsync();
        var changes = await db.Events.AsNoTracking()
            .Where(e => e.ExamSessionId == session.Id && e.Type == EventType.IP_CHANGE)
            .ToListAsync();

        Assert.Equal(First, session.InitialIpAddress);
        var change = Assert.Single(changes);
        Assert.Equal(EventSource.Network, change.Source);
        Assert.Contains("78.180.5.6", change.Payload);
    }

    [SkippableFact]
    public async Task Ipv4_mapped_ipv6_address_is_treated_as_the_same_ip()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db);
        var clock = Clock();
        var guard = new SessionIpGuard(db, Service(db, clock), clock);

        await guard.CheckAsync(session.Id, First);
        await guard.CheckAsync(session.Id, First.MapToIPv6());

        Assert.False(await db.Events.AnyAsync(e => e.ExamSessionId == session.Id));
    }
}
