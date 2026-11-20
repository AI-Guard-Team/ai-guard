using AIGuard.Domain.Entities;

namespace AIGuard.Application.Abstractions;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Erişim belirteci (JWT) üretimi. Uygulaması Infrastructure'da (K-14).</summary>
public interface ITokenService
{
    /// <summary>
    /// Kullanıcı kimliği (sub), rol, e-posta ve ad içeren kısa ömürlü JWT. Önyüz menü ve
    /// yönlendirme için bu talepleri okuyor (web/src/auth/jwt.ts).
    /// </summary>
    AccessToken CreateAccessToken(User user);
}
