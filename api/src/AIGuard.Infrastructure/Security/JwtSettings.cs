using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AIGuard.Infrastructure.Security;

/// <summary>
/// JWT imzalama ayarları (E2). Anahtar JWT_SECRET ortam değişkeninden ya da user-secrets'tan
/// okunuyor (.env.example); koda gömülü varsayılan yok.
/// </summary>
public sealed class JwtSettings
{
    public const string SecretKey = "JWT_SECRET";

    // Belirtecin bu API tarafından bu web uygulaması için üretildiğini gösteriyor; başka bir
    // sistemin aynı anahtarla ürettiği belirteç kabul edilmiyor.
    public const string Issuer = "aiguard-api";
    public const string Audience = "aiguard-web";

    // HMAC-SHA256 için en az 256 bit anahtar.
    public const int MinSecretBytes = 32;

    public required SymmetricSecurityKey SigningKey { get; init; }

    public static JwtSettings FromConfiguration(IConfiguration configuration)
    {
        var secret = configuration[SecretKey];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < MinSecretBytes)
        {
            // Zayıf ya da eksik anahtarla açılmak yerine açılışta duruyor.
            throw new InvalidOperationException(
                $"{SecretKey} tanımlı değil veya {MinSecretBytes} bayttan kısa. Ortam değişkeni ya da " +
                "dotnet user-secrets ile ayarlayın (bkz. README).");
        }

        return new JwtSettings { SigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)) };
    }

    /// <summary>Üretim ve doğrulama aynı parametreleri kullansın diye tek yerde.</summary>
    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = SigningKey,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        // Varsayılan 5 dk; 15 dakikalık belirteçte süre dolduktan sonra 5 dk daha kabul etmek çok.
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = JwtClaimNames.Name,
        RoleClaimType = JwtClaimNames.Role,
    };
}

/// <summary>JWT'deki talep adları; önyüz (web/src/auth/jwt.ts) kısa adları okuyor.</summary>
public static class JwtClaimNames
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string Email = "email";
    public const string Name = "name";
}
