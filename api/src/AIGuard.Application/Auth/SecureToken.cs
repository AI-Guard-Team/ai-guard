using System.Security.Cryptography;
using System.Text;

namespace AIGuard.Application.Auth;

/// <summary>
/// Davet ve yenileme belirteçleri (E2). Belirtecin kendisi yalnızca kullanıcıya gidiyor,
/// veritabanında SHA-256 özeti tutuluyor (K-08). Yöntem eşleştirme koduyla (PairingCode) aynı.
/// </summary>
public static class SecureToken
{
    private const int ByteLength = 32;

    /// <summary>32 baytın dolgu karaktersiz Base64Url hâli.</summary>
    public const int Length = 43;

    public static string Generate()
    {
        // 256 bit güvenli rastgele: tahmin edilemez, bu yüzden özetinde tuz gerekmiyor.
        var bytes = RandomNumberGenerator.GetBytes(ByteLength);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Biçimi bozuk belirteç veritabanına sorgu atmadan reddediliyor.</summary>
    public static bool IsWellFormed(string? token) =>
        token is { Length: Length } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
