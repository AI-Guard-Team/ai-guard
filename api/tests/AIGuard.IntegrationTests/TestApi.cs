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
            // Kanıt karesi ve yüz vektörü şifreleme anahtarı (A3); her test sunucusu için rastgele (E5a).
            b.UseSetting("EVIDENCE_ENCRYPTION_KEY", Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
            // Testler sabit tarihli oturumlar kullanıyor; arka plandaki otomatik teslim onları kapatmasın (E4).
            // Süre denetimi SessionTimekeeper testlerinde FakeTimeProvider ile ayrıca sınanıyor.
            b.UseSetting("Sessions:TimekeeperEnabled", "false");
            // Aynı nedenle saklama süresi görevi de kapalı (A5); DataRetentionServiceTests ayrıca sınıyor.
            b.UseSetting("Retention:Enabled", "false");
            // Sonra eklenen kayıt geçerli: sahte gönderici yerine davetleri biriktiren gönderici.
            b.ConfigureServices(s => s.AddSingleton<IEmailSender>(Emails));
            // Testler kamera karesi göndermeden soruları okuyor; "kare var" kabul ediliyor. Kuralın kendisi
            // (S-29) SessionQuestionGateTests'te gerçek hız sınırıyla sınanıyor.
            b.ConfigureServices(s => s.AddSingleton<AIGuard.Application.Sessions.ICameraPresence>(new AlwaysPresentCamera()));
            // Testin kendi sahte servisleri (ör. S4'te analizci); sonra eklendiği için geçerli olan bunlar.
            if (services is not null)
            {
                b.ConfigureServices(services);
            }
        });
    }

    private sealed class AlwaysPresentCamera : AIGuard.Application.Sessions.ICameraPresence
    {
        public bool HasRecentFrame(Guid sessionId, DateTimeOffset now) => true;
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

    private readonly ConcurrentDictionary<string, List<string>> _resetTokensByEmail = new(StringComparer.OrdinalIgnoreCase);

    public Task SendPasswordResetAsync(string email, string fullName, string resetToken, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        _resetTokensByEmail.AddOrUpdate(email, _ => [resetToken], (_, list) => { lock (list) { list.Add(resetToken); } return list; });
        return Task.CompletedTask;
    }

    /// <summary>Bu adrese giden sıfırlama belirteçleri, gönderim sırasıyla.</summary>
    public IReadOnlyList<string> ResetTokensFor(string email) =>
        _resetTokensByEmail.TryGetValue(email, out var list) ? list.ToList() : [];
}
