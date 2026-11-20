using System.Net;
using System.Text;
using AIGuard.Agent.Core.Pairing;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIGuard.Agent.Core.Tests;

/// <summary>Ajanın kodu sunucuya iletmesi.</summary>
public class HttpPairingClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private static HttpPairingClient Client(StubHandler handler, string baseAddress = "https://api.ornek.edu.tr/") =>
        new(new HttpClient(handler) { BaseAddress = new Uri(baseAddress) }, NullLogger<HttpPairingClient>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Code_is_posted_as_json_to_the_server_pair_endpoint()
    {
        var pairingId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, $$"""{"pairingId":"{{pairingId}}","sessionId":"{{sessionId}}"}"""));

        var outcome = await Client(handler).PairAsync("kod123", "Windows");

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.ornek.edu.tr/api/agent/pair", handler.Request.RequestUri!.ToString());
        Assert.Equal("""{"code":"kod123","os":"Windows"}""", handler.Body);
        Assert.Equal(new PairingOutcome(PairingStatus.Paired, pairingId, sessionId), outcome);
    }

    [Fact]
    public async Task Server_path_prefix_is_kept()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

        await Client(handler, "https://ornek.edu.tr/aiguard/").PairAsync("kod123", "Windows");

        Assert.Equal("https://ornek.edu.tr/aiguard/api/agent/pair", handler.Request!.RequestUri!.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, PairingStatus.Rejected)]
    [InlineData(HttpStatusCode.NotFound, PairingStatus.Rejected)]
    [InlineData(HttpStatusCode.InternalServerError, PairingStatus.Unavailable)]
    [InlineData(HttpStatusCode.BadGateway, PairingStatus.Unavailable)]
    public async Task Server_status_is_mapped(HttpStatusCode status, PairingStatus expected)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status));

        var outcome = await Client(handler).PairAsync("kod123", "Windows");

        Assert.Equal(expected, outcome.Status);
    }

    [Fact]
    public async Task Connection_or_tls_failure_means_unavailable()
    {
        // Sertifika doğrulama hatası da HttpRequestException olarak geliyor.
        var handler = new StubHandler(_ => throw new HttpRequestException("TLS el sıkışması başarısız"));

        var outcome = await Client(handler).PairAsync("kod123", "Windows");

        Assert.Equal(PairingStatus.Unavailable, outcome.Status);
    }

    [Fact]
    public async Task Unexpected_success_body_means_unavailable()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "beklenmeyen"));

        var outcome = await Client(handler).PairAsync("kod123", "Windows");

        Assert.Equal(PairingStatus.Unavailable, outcome.Status);
    }
}
