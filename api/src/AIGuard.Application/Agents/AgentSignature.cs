using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIGuard.Application.Agents;

/// <summary>
/// Ajan sinyallerinin HMAC imzası (Y2, FR-21).
/// </summary>
/// <remarks>
/// <c>sig = Base64(HMAC-SHA256(anahtar, "{sessionId}|{ts}|{nonce}|{indicatorsJson}"))</c>
/// <list type="bullet">
/// <item><c>sessionId</c>: Guid, küçük harf, tireli ("D" biçimi)</item>
/// <item><c>ts</c>: Unix zamanı, saniye</item>
/// <item><c>indicatorsJson</c>: gösterge dizisinin System.Text.Json varsayılan ayarlarıyla sıkışık JSON'u (ör. <c>["VM_DETECTED"]</c>, boşsa <c>[]</c>)</item>
/// </list>
/// Ajandaki <c>SignalSigner</c> aynı biçimi üretiyor; iki taraftaki <c>Known_answer</c> testi aynı
/// sabit örnekte aynı imzayı bekliyor. Biçim değişirse ikisi birlikte değiştirilmeli.
/// </remarks>
public static class AgentSignature
{
    public const int KeyLength = 32;

    /// <summary>Eşleşmede üretilen, oturuma özel 32 baytlık rastgele anahtar.</summary>
    public static byte[] GenerateKey() => RandomNumberGenerator.GetBytes(KeyLength);

    /// <summary>Veritabanına yazılan özet (K-08): veritabanı okunsa bile anahtar elde edilemiyor.</summary>
    public static string HashKey(byte[] key) => Convert.ToHexString(SHA256.HashData(key));

    public static string CanonicalMessage(Guid sessionId, long ts, string nonce, IReadOnlyList<string> indicators) =>
        string.Join('|',
            sessionId.ToString("D"),
            ts.ToString(CultureInfo.InvariantCulture),
            nonce,
            JsonSerializer.Serialize(indicators));

    public static string Sign(byte[] key, Guid sessionId, long ts, string nonce, IReadOnlyList<string> indicators) =>
        Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(CanonicalMessage(sessionId, ts, nonce, indicators))));

    public static bool Verify(byte[] key, Guid sessionId, long ts, string nonce, IReadOnlyList<string> indicators, string? signature)
    {
        Span<byte> received = stackalloc byte[HMACSHA256.HashSizeInBytes];
        if (signature is null || !Convert.TryFromBase64String(signature, received, out var written)
            || written != HMACSHA256.HashSizeInBytes)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(CanonicalMessage(sessionId, ts, nonce, indicators)));

        // Sabit zamanlı karşılaştırma (kart): ilk farklı baytta erken dönen karşılaştırma, yanıt
        // süresinden imzanın kaç baytının doğru olduğunu sızdırır.
        return CryptographicOperations.FixedTimeEquals(expected, received);
    }
}
