using AIGuard.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace AIGuard.Infrastructure.Email;

/// <summary>SMTP ayarları; ortam değişkenlerinden (.env.example). <c>SMTP_HOST</c> boşsa gerçek gönderim yok.</summary>
public sealed record SmtpSettings(string Host, int Port, string? Username, string? Password, string From, SecureSocketOptions Security)
{
    public static SmtpSettings? FromConfiguration(IConfiguration configuration)
    {
        var host = configuration["SMTP_HOST"];
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var port = int.TryParse(configuration["SMTP_PORT"], out var p) && p is > 0 and < 65536 ? p : 587;
        var security = (configuration["SMTP_SECURITY"] ?? "StartTls").Trim().ToLowerInvariant() switch
        {
            "none" => SecureSocketOptions.None,
            "ssl" or "sslonconnect" => SecureSocketOptions.SslOnConnect,
            _ => SecureSocketOptions.StartTls,
        };
        var from = configuration["SMTP_FROM"];
        if (string.IsNullOrWhiteSpace(from) || !MailboxAddress.TryParse(from, out _))
        {
            throw new InvalidOperationException("SMTP_HOST tanımlıyken SMTP_FROM geçerli bir gönderen adresi olmalı (ör. \"AI-Guard <no-reply@kurum.edu.tr>\").");
        }

        return new SmtpSettings(host.Trim(), port, configuration["SMTP_USERNAME"], configuration["SMTP_PASSWORD"], from, security);
    }
}

/// <summary>
/// Gerçek e-posta gönderici (K-49, S-15 kapandı). Bağlantı bir kimlik bilgisi olduğu için loga yazılmaz; yalnızca
/// alıcı ve sonuç yazılır. Gönderim hatası çağıranı düşürmez: kullanıcı oluşmuş olur, yönetici daveti yeniden
/// gönderebilir; şifre sıfırlamada yanıt yine 202 (e-postanın kayıtlı olup olmadığı sızmasın).
/// </summary>
public sealed class SmtpEmailSender(SmtpSettings settings, IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public Task SendInviteAsync(string email, string fullName, string inviteToken, DateTimeOffset expiresAt, CancellationToken ct = default) =>
        SendAsync(email, fullName, EmailTemplates.Invite(fullName, EmailTemplates.Link(configuration, "set-password", inviteToken), expiresAt), "davet", ct);

    public Task SendPasswordResetAsync(string email, string fullName, string resetToken, DateTimeOffset expiresAt, CancellationToken ct = default) =>
        SendAsync(email, fullName, EmailTemplates.PasswordReset(fullName, EmailTemplates.Link(configuration, "reset-password", resetToken), expiresAt), "şifre sıfırlama", ct);

    private async Task SendAsync(string email, string fullName, EmailMessage content, string kind, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(settings.From));
        message.To.Add(new MailboxAddress(fullName, email));
        message.Subject = content.Subject;
        message.Body = new BodyBuilder { TextBody = content.Text, HtmlBody = content.Html }.ToMessageBody();

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);
            using var client = new SmtpClient { Timeout = (int)Timeout.TotalMilliseconds };
            await client.ConnectAsync(settings.Host, settings.Port, settings.Security, timeout.Token);
            if (!string.IsNullOrEmpty(settings.Username))
            {
                await client.AuthenticateAsync(settings.Username, settings.Password ?? "", timeout.Token);
            }

            await client.SendAsync(message, timeout.Token);
            await client.DisconnectAsync(true, timeout.Token);
            logger.LogInformation("E-posta gönderildi ({Kind}) → {Email}", kind, email);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Bağlantı ve belirteç loga yazılmıyor; yalnızca neden.
            logger.LogError(ex, "E-posta gönderilemedi ({Kind}) → {Email}", kind, email);
        }
    }
}
