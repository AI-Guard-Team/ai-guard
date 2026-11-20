using AIGuard.Application.Abstractions;
using AIGuard.Application.Auth;
using AIGuard.Domain.Entities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AIGuard.Infrastructure.Security;

/// <summary>15 dakikalık, HMAC-SHA256 ile imzalı erişim belirteci (E2, FR-02).</summary>
public sealed class JwtTokenService(JwtSettings settings, TimeProvider clock) : ITokenService
{
    // Zamanları TimeProvider'dan biz veriyoruz (K-15); işleyici kendi saatini eklemesin.
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };

    public AccessToken CreateAccessToken(User user)
    {
        var now = clock.GetUtcNow();
        var expires = now + AuthPolicy.AccessTokenLifetime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = JwtSettings.Issuer,
            Audience = JwtSettings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(settings.SigningKey, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtClaimNames.Subject] = user.Id.ToString(),
                // Enum adıyla ("Admin"); [Authorize(Roles = "Admin")] ve önyüz bu yazımı bekliyor.
                [JwtClaimNames.Role] = user.Role.ToString(),
                [JwtClaimNames.Email] = user.Email,
                [JwtClaimNames.Name] = user.FullName,
                // Her belirteç benzersiz; aynı saniyede üretilen iki belirteç aynı çıkmasın.
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
        };

        return new AccessToken(_handler.CreateToken(descriptor), expires);
    }
}
