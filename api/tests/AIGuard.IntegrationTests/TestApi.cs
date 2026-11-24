using System.Collections.Concurrent;
using System.Net.Http.Headers;
using AIGuard.Application.Abstractions;
using AIGuard.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AIGuard.IntegrationTests;

/// <summary>
/// HTTP testleri için API (E2'den beri her uç giriş istiyor). Geçici test veritabanına bağlanıyor,
/// sabit bir test JWT anahtarı kullanıyor ve davet e-postalarını yakalıyor.
/// </summary>
internal sealed class TestApi
{
    // Yalnızca testler için; gerçek ortamda JWT_SECRET ortam değişkeninden geliyor.
    public const string JwtSecret = "integration-tests-only-jwt-secret-0123456789";

    public TestApi(PostgresFixture fx, string environment = "Development", Action<IServiceCollection>? services = null)
    {
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment(environment);
            b.UseSetting("ConnectionStrings:Default", fx.ConnectionString);
            b.UseSetting("JWT_SECRET", JwtSecret);
            // Testler sabit tarihli oturumlar kullanıyor; arka plandaki otomatik teslim onları kapatmasın (E4).
            // Süre denetimi SessionTimekeeper testlerinde FakeTimeProvider ile ayrıca sınanıyor.
            b.UseSetting("Sessions:TimekeeperEnabled", "false");
            // Sonra eklenen kayıt geçerli: sahte gönderici yerine davetleri biriktiren gönderici.
            b.ConfigureServices(s => s.AddSingleton<IEmailSender>(Emails));
            // Testin kendi sahte servisleri (ör. S4'te analizci); sonra eklendiği için geçerli olan bunlar.
            if (services is not null)
            {
                b.ConfigureServices(services);
            }
        });
    }

    public WebApplicationFactory<Program> Factory { get; }

    public CapturingEmailSender Emails { get; } = new();

    public HttpClient Anonymous() => Factory.CreateClient();

    /// <summary>Kullanıcı adına geçerli bir erişim belirteciyle istemci. Kullanıcı veritabanında kayıtlı olmalı.</summary>
    public HttpClient As(User user) => WithToken(Factory.Services.GetRequiredService<ITokenService>().CreateAccessToken(user).Token);

    public HttpClient WithToken(string accessToken)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}

/// <summary>Davet bağlantısını loga yazmak yerine testin okuyabileceği yerde tutar.</summary>
internal sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentDictionary<string, string> _tokensByEmail = new(StringComparer.OrdinalIgnoreCase);

    public Task SendInviteAsync(string email, string fullName, string inviteToken, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        _tokensByEmail[email] = inviteToken;
        return Task.CompletedTask;
    }

    public string InviteTokenFor(string email) => _tokensByEmail[email];
}
