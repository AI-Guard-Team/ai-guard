using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIGuard.Agent.Core.Tests;

/// <summary>Gerçek Kestrel ile: ajan yalnızca 127.0.0.1'i dinliyor.</summary>
public class LoopbackBindingTests
{
    [Fact]
    public async Task Agent_listens_only_on_ipv4_loopback()
    {
        var builder = AgentHost.CreateBuilder([]);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Agent:Port"] = "0", // testte sabit 47321 yerine boş port; çalışan bir ajanla çakışmasın
            ["Agent:ApiBaseUrl"] = "https://api.ornek.edu.tr",
            ["Agent:AllowedOrigins:0"] = TestAgent.PlatformOrigin,
            // Ortamdan gelebilecek ayar: Kestrel'in bunu yok saydığını da doğruluyor.
            ["urls"] = "http://0.0.0.0:0",
        });
        builder.Services.AddSingleton<AIGuard.Agent.Core.Platform.IDeviceInspector, FakeDeviceInspector>();
        await using var app = AgentHost.Build(builder);
        await app.StartAsync();

        var addresses = app.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses;

        var address = Assert.Single(addresses);
        var uri = new Uri(address);
        Assert.Equal("127.0.0.1", uri.Host);

        // Aynı porta bilgisayarın ağ adresinden bağlanılamamalı (yalnızca ağ kartı varsa denenir).
        var lanAddress = (await Dns.GetHostAddressesAsync(Dns.GetHostName()))
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
        if (lanAddress is not null)
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await Assert.ThrowsAsync<SocketException>(() => socket.ConnectAsync(lanAddress, uri.Port));
        }

        await app.StopAsync();
    }
}
