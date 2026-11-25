namespace AIGuard.Application.Abstractions;

/// <summary>
/// E-posta gönderimi. Kart E2: şimdilik sahte gönderici kullanılıyor ve bağlantı loga yazılıyor
/// (Infrastructure/Email/LoggingEmailSender). Gerçek gönderici gelirse yalnızca uygulama değişir.
/// </summary>
public interface IEmailSender
{
    /// <param name="inviteToken">Davet belirtecinin kendisi; bağlantıyı gönderici kuruyor.</param>
    Task SendInviteAsync(string email, string fullName, string inviteToken, DateTimeOffset expiresAt, CancellationToken ct = default);

    /// <param name="resetToken">Şifre sıfırlama belirtecinin kendisi; bağlantıyı gönderici kuruyor.</param>
    Task SendPasswordResetAsync(string email, string fullName, string resetToken, DateTimeOffset expiresAt, CancellationToken ct = default);
}
