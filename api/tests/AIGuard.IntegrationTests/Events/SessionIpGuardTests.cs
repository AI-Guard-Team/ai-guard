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
    private static VpnIpBlockList Blocks() => new(Microsoft.Extensions.Options.Options.Create(new VpnIpBlockOptions
    {
        Blocks = [new VpnIpBlock("198.51.100.0/24", "Örnek VPN")],
    }));

    [SkippableFact]
    public async Task Ip_in_a_known_vpn_block_is_reported_once_as_vpn_proxy()
    {
        // Y4, FR-23: ilk IP bilinen bir VPN bloğunda; aynı IP'yle gelen sonraki isteklerde tekrar olay yok.
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db);
        var clock = Clock();
        var guard = new SessionIpGuard(db, Service(db, clock), clock, Blocks());
        var vpnIp = IPAddress.Parse("198.51.100.7");

        await guard.CheckAsync(session.Id, vpnIp);
        await guard.CheckAsync(session.Id, vpnIp);

        var ev = Assert.Single(await db.Events.AsNoTracking().Where(e => e.ExamSessionId == session.Id).ToListAsync());
        Assert.Equal((EventType.VPN_PROXY, EventSource.Network), (ev.Type, ev.Source));
        Assert.Contains("198.51.100.0/24", ev.Payload);
        Assert.Contains("Örnek VPN", System.Text.RegularExpressions.Regex.Unescape(ev.Payload!));
    }

    [SkippableFact]
    public async Task Switching_to_a_vpn_ip_mid_exam_gives_ip_change_and_vpn_proxy()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db);
        var clock = Clock();
        var guard = new SessionIpGuard(db, Service(db, clock), clock, Blocks());

        await guard.CheckAsync(session.Id, First);
        await guard.CheckAsync(session.Id, IPAddress.Parse("198.51.100.200"));

        var types = await db.Events.AsNoTracking().Where(e => e.ExamSessionId == session.Id).Select(e => e.Type).ToListAsync();
        Assert.Equal(new[] { EventType.IP_CHANGE, EventType.VPN_PROXY }.Order(), types.Order());
    }

    [SkippableFact]
    public async Task Ordinary_ip_produces_no_vpn_event()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db);
        var clock = Clock();

        await new SessionIpGuard(db, Service(db, clock), clock, Blocks()).CheckAsync(session.Id, First);

        Assert.False(await db.Events.AnyAsync(e => e.ExamSessionId == session.Id));
    }
}
