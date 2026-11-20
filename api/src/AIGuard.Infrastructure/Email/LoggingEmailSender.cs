using AIGuard.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIGuard.Infrastructure.Email;

/// <summary>
/// Sahte e-posta gönderici (kart E2): e-posta göndermek yerine davet bağlantısını loga yazıyor.
/// Geliştirme ve deneme içindir; bağlantı bir kimlik bilgisi olduğundan gerçek kullanımda
/// SMTP gibi bir gönderici ile değiştirilmeli (MIMARI-KARARLAR K-20).
/// </summary>
public sealed class LoggingEmailSender(IConfiguration configuration, ILogger<LoggingEmailSender> logger) : IEmailSender
{
    // Vite geliştirme sunucusunun varsayılan adresi; Docker'da Web:BaseUrl ile değiştirilir.
    private const string DefaultWebBaseUrl = "http://localhost:5173";

    public Task SendInviteAsync(string email, string fullName, string inviteToken, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        var baseUrl = (configuration["Web:BaseUrl"] ?? DefaultWebBaseUrl).TrimEnd('/');
        // Önyüzdeki şifre belirleme sayfası (S1): /set-password?token=
        var link = $"{baseUrl}/set-password?token={Uri.EscapeDataString(inviteToken)}";

        logger.LogInformation(
            "[Sahte e-posta] Davet: {FullName} <{Email}> · bağlantı: {Link} · son geçerlilik: {ExpiresAt:u}",
            fullName, email, link, expiresAt);
        return Task.CompletedTask;
    }
}
