using AIGuard.Agent.Core.Pairing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIGuard.Agent.Core.Tests;

/// <summary>Sunucuya gitmeden sonucu testin belirlediği istemci.</summary>
internal sealed class FakePairingClient : IPairingClient
{
    public PairingOutcome Outcome { get; set; } = new(PairingStatus.Paired, Guid.NewGuid(), Guid.NewGuid());

    public List<string> ReceivedCodes { get; } = [];

    public Task<PairingOutcome> PairAsync(string code, CancellationToken cancellationToken = default)
    {
        ReceivedCodes.Add(code);
        return Task.FromResult(Outcome);
    }
}

/// <summary>Bellek içi test sunucusunda çalışan ajan.</summary>
internal sealed class TestAgent : IAsyncDisposable
{
    public const string PlatformOrigin = "https://sinav.ornek.edu.tr";

    // Sunucunun ürettiği kodla aynı biçim (43 karakter Base64Url).
    public const string ValidCode = "AbCdEfGhIjKlMnOpQrStUvWxYz0123456789-_AbCdE";

    private TestAgent(WebApplication app, FakePairingClient client)
    {
        App = app;
        Client = client;
        Http = app.GetTestClient();
        // Tarayıcı http://127.0.0.1:47321 adresine istek attığında Host başlığı bu oluyor.
        Http.BaseAddress = new Uri("http://127.0.0.1:47321");
    }

    public WebApplication App { get; }

    public FakePairingClient Client { get; }

    public HttpClient Http { get; }

    public static async Task<TestAgent> StartAsync()
    {
        var builder = AgentHost.CreateBuilder([]);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Agent:ApiBaseUrl"] = "https://api.ornek.edu.tr",
            ["Agent:AllowedOrigins:0"] = PlatformOrigin,
        });
        builder.WebHost.UseTestServer();

        var client = new FakePairingClient();
        builder.Services.AddSingleton<IPairingClient>(client); // gerçek HTTP istemcisinin yerine

        var app = AgentHost.Build(builder);
        await app.StartAsync();
        return new TestAgent(app, client);
    }

    public static HttpRequestMessage Pair(string? origin, string? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/pair")
        {
            Content = new StringContent(body ?? $$"""{"code":"{{ValidCode}}"}""", System.Text.Encoding.UTF8, "application/json"),
        };
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }
        return request;
    }

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        await App.DisposeAsync();
    }
}
