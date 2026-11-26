using AIGuard.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIGuard.Infrastructure.Email;

/// <summary>
/// Sahte e-posta gönderici (kart E2): e-posta göndermek yerine davet bağlantısını loga yazıyor.
/// <c>SMTP_HOST</c> tanımlı değilken kullanılıyor (geliştirme ve CI); gerçek gönderim <see cref="SmtpEmailSender"/> (K-49).
/// </summary>
public sealed class LoggingEmailSender(IConfiguration configuration, ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendInviteAsync(string email, string fullName, string inviteToken, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        // Önyüzdeki şifre belirleme sayfası (S1): /set-password?token=
        var link = EmailTemplates.Link(configuration, "set-password", inviteToken);

        logger.LogInformation(
            "[Sahte e-posta] Davet: {FullName} <{Email}> · bağlantı: {Link} · son geçerlilik: {ExpiresAt:u}",
            fullName, email, link, expiresAt);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string fullName, string resetToken, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        // Önyüzdeki şifre sıfırlama sayfası: /reset-password?token=
        var link = EmailTemplates.Link(configuration, "reset-password", resetToken);

        logger.LogInformation(
            "[Sahte e-posta] Şifre sıfırlama: {FullName} <{Email}> · bağlantı: {Link} · son geçerlilik: {ExpiresAt:u}",
            fullName, email, link, expiresAt);
        return Task.CompletedTask;
    }
}
