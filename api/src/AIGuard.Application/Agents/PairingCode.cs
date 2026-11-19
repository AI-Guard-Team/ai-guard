using System.Security.Cryptography;
using System.Text;

namespace AIGuard.Application.Agents;

/// <summary>
/// Tarayıcı ile ajanı eşleştiren tek kullanımlık kod (Y1, FR-11).
/// </summary>
/// <remarks>
/// Kod yalnızca tarayıcıdan aynı bilgisayardaki ajana (127.0.0.1) gidiyor. Başka bir
/// bilgisayardaki ajan kodu ancak tahmin ederek bulabilir; 256 bitlik rastgele değer bunu
/// pratikte imkânsız kılıyor, bu yüzden ayrıca deneme sayısı sınırı gerekmiyor.
/// </remarks>
public static class PairingCode
{
    public const int ByteLength = 32;

    /// <summary>32 baytın dolgu karaktersiz Base64Url hâli: 43 karakter.</summary>
    public const int Length = 43;

    public static string Generate()
    {
        // RandomNumberGenerator: şifreleme amaçlı güvenli rastgele (Random tahmin edilebilir).
        var bytes = RandomNumberGenerator.GetBytes(ByteLength);

        // Base64Url: URL ve JSON'da kaçış gerektirmiyor.
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// Veritabanında kodun kendisi değil SHA-256 özeti tutuluyor (K-08): veritabanı
    /// okunsa bile geçerli bir kod elde edilemiyor. Kod uzun ve rastgele olduğu için tuz gerekmiyor.
    /// </summary>
    public static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    /// <summary>Biçimi bozuk kod veritabanına sorgu atmadan reddediliyor.</summary>
    public static bool IsWellFormed(string? code) =>
        code is { Length: Length } && code.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
