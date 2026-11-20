using System.Net;
using AIGuard.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AIGuard.UnitTests;

public class TrustedProxySetupTests
{
    private static IServiceCollection Services(params string[] networks)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(networks.Select((n, i) =>
                new KeyValuePair<string, string?>($"{TrustedProxySetup.SectionName}:{i}", n)))
            .Build();
        var services = new ServiceCollection();
        services.AddTrustedProxies(config);
        return services;
    }

    /// <summary>ForwardedHeaders ara katmanını verilen bağlantı adresiyle çalıştırıp sonucu döner.</summary>
    private static async Task<IPAddress?> ClientIpSeenByApiAsync(IServiceCollection services, string connectionIp, string forwardedFor)
    {
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<ForwardedHeadersOptions>>();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(connectionIp);
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;

        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, options);
        await middleware.Invoke(context);
        return context.Connection.RemoteIpAddress;
    }

    [Fact]
    public void Without_configuration_nothing_is_enabled()
    {
        var services = new ServiceCollection();

        Assert.False(services.AddTrustedProxies(new ConfigurationBuilder().Build()));
    }

    [Fact]
    public async Task Forwarded_ip_is_accepted_from_the_trusted_proxy()
    {
        var ip = await ClientIpSeenByApiAsync(Services("172.30.47.10/32"), "172.30.47.10", "203.0.113.5");

        Assert.Equal(IPAddress.Parse("203.0.113.5"), ip);
    }

    [Fact]
    public async Task Forwarded_ip_is_ignored_from_any_other_address()
    {
        // Aday doğrudan API'ye başlık yazarak IP değişimini gizleyemez.
        var ip = await ClientIpSeenByApiAsync(Services("172.30.47.10/32"), "198.51.100.7", "203.0.113.5");

        Assert.Equal(IPAddress.Parse("198.51.100.7"), ip);
    }

    [Fact]
    public async Task Client_written_addresses_before_the_proxy_are_ignored()
    {
        // İstemci kendi X-Forwarded-For'unu yazarsa nginx onun sonuna gerçek adresi ekler: "sahte, gerçek".
        var ip = await ClientIpSeenByApiAsync(Services("172.30.47.10/32"), "172.30.47.10", "1.1.1.1, 203.0.113.5");

        Assert.Equal(IPAddress.Parse("203.0.113.5"), ip);
    }

    [Fact]
    public void Invalid_network_stops_startup()
    {
        Assert.Throws<InvalidOperationException>(() => Services("web-sunucusu"));
    }
}
