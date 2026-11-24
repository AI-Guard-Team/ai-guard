using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AIGuard.Application.Review;

/// <summary>
/// Kanıt karesi için kısa ömürlü imzalı bağlantı (A4, K-31). Değerlendirici ekranı kareyi
/// <c>&lt;img src&gt;</c> ile açıyor; tarayıcı bu istekte Authorization başlığı gönderemediği için
/// yetki bağlantının içindeki imzayla taşınıyor. Bağlantıyı yalnızca yetki kontrolünden geçen
/// detay ucu üretiyor.
/// </summary>
/// <remarks>
/// Anahtar her açılışta rastgele üretilip yalnızca bellekte tutuluyor: bağlantılar zaten birkaç dakikalık,
/// API yeniden başlarsa ekran detayı yeniden isteyip yeni bağlantı alıyor. Proje tek API örneğiyle
/// çalışıyor (S-17); birden fazla örnek olursa anahtar ortak bir yerden okunmalı.
/// </remarks>
public sealed class EvidenceLinkSigner(TimeProvider clock)
{
    /// <summary>Bağlantının geçerlilik süresi. Kart "kısa ömürlü" diyor; detay ekranını açıp inceleme için yeterli.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);

    public string CreateUrl(Guid frameId)
    {
        var expires = clock.GetUtcNow().Add(Lifetime).ToUnixTimeSeconds();
        return string.Create(CultureInfo.InvariantCulture,
            $"/api/evidence/{frameId}?exp={expires}&sig={ToBase64Url(Sign(frameId, expires))}");
    }

    public bool IsValid(Guid frameId, long expires, string? signature)
    {
        var now = clock.GetUtcNow().ToUnixTimeSeconds();

        // Süresi geçmiş ya da ömründen uzun bir son tarih taşıyan bağlantı reddediliyor.
        if (expires < now || expires > now + (long)Lifetime.TotalSeconds || string.IsNullOrEmpty(signature))
        {
            return false;
        }

        if (FromBase64Url(signature) is not { } given)
        {
            return false;
        }

        // Sabit zamanlı karşılaştırma: imzanın kaç baytının tuttuğu süreden anlaşılamasın.
        return CryptographicOperations.FixedTimeEquals(given, Sign(frameId, expires));
    }

    private byte[] Sign(Guid frameId, long expires) =>
        HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"evidence|{frameId:N}|{expires}")));

    // Sorgu dizesinde kaçış gerektirmesin diye base64url (+ → -, / → _, dolgu yok).
    private static string ToBase64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[]? FromBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        var buffer = new byte[base64.Length];
        return Convert.TryFromBase64String(base64, buffer, out var written) ? buffer[..written] : null;
    }
}
