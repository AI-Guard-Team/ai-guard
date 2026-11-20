using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIGuard.Agent.Core.Signals;

/// <summary>Sunucuya giden sinyal gövdesi: POST /api/agent/signal (Y2).</summary>
public sealed record SignalRequest(Guid SessionId, long Ts, string Nonce, IReadOnlyList<string> Indicators, string Sig);

/// <summary>
/// Ajan sinyallerinin HMAC imzası (Y2, FR-21).
/// </summary>
/// <remarks>
/// <c>sig = Base64(HMAC-SHA256(anahtar, "{sessionId}|{ts}|{nonce}|{indicatorsJson}"))</c>; sessionId küçük harf
/// tireli Guid, ts Unix saniye, indicatorsJson System.Text.Json varsayılanıyla sıkışık dizi.
/// Sunucudaki <c>AgentSignature</c> ile birebir aynı olmalı: iki taraftaki Known_answer testi aynı
/// sabit örnekte aynı imzayı bekliyor.
/// </remarks>
public static class SignalSigner
{
    public static string CanonicalMessage(Guid sessionId, long ts, string nonce, IReadOnlyList<string> indicators) =>
        string.Join('|',
            sessionId.ToString("D"),
            ts.ToString(CultureInfo.InvariantCulture),
            nonce,
            JsonSerializer.Serialize(indicators));

    public static string Sign(byte[] key, Guid sessionId, long ts, string nonce, IReadOnlyList<string> indicators) =>
        Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(CanonicalMessage(sessionId, ts, nonce, indicators))));

    /// <summary>Tek kullanımlık değer: 128 bit rastgele, Base64Url (tekrar oynatma kontrolü için).</summary>
    public static string NewNonce() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static SignalRequest Create(byte[] key, Guid sessionId, DateTimeOffset now, IReadOnlyList<string> indicators)
    {
        var ts = now.ToUnixTimeSeconds();
        var nonce = NewNonce();
        return new SignalRequest(sessionId, ts, nonce, indicators, Sign(key, sessionId, ts, nonce, indicators));
    }
}
