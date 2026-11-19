using System.Net;
using System.Text;
using AIGuard.Agent.Core.Pairing;
using Microsoft.Extensions.DependencyInjection;
using static AIGuard.Agent.Core.Tests.TestAgent;

namespace AIGuard.Agent.Core.Tests;

/// <summary>http://127.0.0.1:47321/pair ucunun tarayıcıya karşı davranışı.</summary>
public class LocalPairingEndpointTests
{
    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/pair");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        request.Headers.Add("Access-Control-Request-Private-Network", "true");
        return request;
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    [Fact]
    public async Task Preflight_from_platform_origin_allows_post_and_private_network_access()
    {
        await using var agent = await StartAsync();

        var response = await agent.Http.SendAsync(Preflight(PlatformOrigin));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(PlatformOrigin, Header(response, "Access-Control-Allow-Origin"));
        Assert.Equal("POST", Header(response, "Access-Control-Allow-Methods"));
        Assert.Equal("Content-Type", Header(response, "Access-Control-Allow-Headers"));
        Assert.Equal("true", Header(response, "Access-Control-Allow-Private-Network"));
        Assert.Equal("Origin", Header(response, "Vary"));
    }

    [Theory]
    [InlineData("https://kotu-site.example")]
    [InlineData("https://sinav.ornek.edu.tr.kotu-site.example")] // alan adının önekini taklit
    [InlineData("http://sinav.ornek.edu.tr")]                    // https yerine http
    [InlineData("null")]                                          // sandbox iframe / yerel dosya
    public async Task Preflight_from_other_origins_is_refused_without_cors_headers(string origin)
    {
        await using var agent = await StartAsync();

        var response = await agent.Http.SendAsync(Preflight(origin));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(Header(response, "Access-Control-Allow-Origin"));
        Assert.Null(Header(response, "Access-Control-Allow-Private-Network"));
    }

    [Fact]
    public async Task Code_from_platform_origin_is_forwarded_to_the_server()
    {
        await using var agent = await StartAsync();

        var response = await agent.Http.SendAsync(Pair(PlatformOrigin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PlatformOrigin, Header(response, "Access-Control-Allow-Origin"));
        Assert.Equal([ValidCode], agent.Client.ReceivedCodes);
        // İşletim sistemini tarayıcı değil, ajanın platform modülü bildiriyor.
        Assert.Equal(["MacOS"], agent.Client.ReceivedOs);
        Assert.Contains("paired", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Successful_pairing_is_remembered_for_signals()
    {
        await using var agent = await StartAsync();

        await agent.Http.SendAsync(Pair(PlatformOrigin));

        var state = agent.App.Services.GetRequiredService<PairingState>();
        Assert.Equal(agent.Client.Outcome, state.Current);
    }

    [Theory]
    [InlineData("https://kotu-site.example")]
    [InlineData(null)] // tarayıcı dışından (ör. curl) gelen istek
    public async Task Code_from_other_origins_is_not_forwarded(string? origin)
    {
        await using var agent = await StartAsync();

        var response = await agent.Http.SendAsync(Pair(origin));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(agent.Client.ReceivedCodes);
    }

    [Fact]
    public async Task Request_with_foreign_host_header_is_refused_dns_rebinding()
    {
        await using var agent = await StartAsync();
        var request = Pair(PlatformOrigin);
        // Kötü niyetli site kendi alan adını 127.0.0.1'e çözdürdüğünde Host bu olur.
        request.Headers.Host = "kotu-site.example:47321";

        var response = await agent.Http.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(agent.Client.ReceivedCodes);
    }

    [Theory]
    [InlineData("""{"code":""}""")]
    [InlineData("""{"code":"kisa"}""")]
    [InlineData("""{"code":"bosluk iceren bir kod degeri 1234567890"}""")]
    [InlineData("""{"code":null}""")]
    [InlineData("""{}""")]
    public async Task Malformed_code_is_not_forwarded(string body)
    {
        await using var agent = await StartAsync();

        var response = await agent.Http.SendAsync(Pair(PlatformOrigin, body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(agent.Client.ReceivedCodes);
    }

    [Fact]
    public async Task Non_json_body_is_not_forwarded()
    {
        await using var agent = await StartAsync();
        var request = Pair(PlatformOrigin);
        // text/plain "basit istek" sayılır, tarayıcı ön kontrol yapmaz; bu yüzden kabul edilmiyor.
        request.Content = new StringContent(ValidCode, Encoding.UTF8, "text/plain");

        var response = await agent.Http.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Empty(agent.Client.ReceivedCodes);
    }

    [Theory]
    [InlineData(PairingStatus.Rejected, HttpStatusCode.Conflict)]
    [InlineData(PairingStatus.Unavailable, HttpStatusCode.BadGateway)]
    public async Task Server_result_is_reported_to_the_browser_without_details(PairingStatus status, HttpStatusCode expected)
    {
        await using var agent = await StartAsync();
        agent.Client.Outcome = new PairingOutcome(status);

        var response = await agent.Http.SendAsync(Pair(PlatformOrigin));

        Assert.Equal(expected, response.StatusCode);
        Assert.Null(agent.App.Services.GetRequiredService<PairingState>().Current);
    }

    [Fact]
    public async Task Other_paths_do_not_exist()
    {
        await using var agent = await StartAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Origin", PlatformOrigin);

        var response = await agent.Http.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
