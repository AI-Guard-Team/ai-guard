using System.Security.Cryptography;
using System.Text;
using AIGuard.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace AIGuard.Infrastructure.Security;

/// <summary>
/// AES-256-GCM ile alan şifreleme (K-26). Çıktı biçimi: nonce (12 bayt) | etiket (16 bayt) | şifreli veri.
/// </summary>
/// <remarks>
/// GCM hem gizlilik hem bütünlük sağlıyor: bir bayt değişirse ya da bağlam (associated data) farklıysa
/// çözme hata veriyor. Her şifrelemede yeni rastgele nonce üretiliyor; aynı anahtarla nonce
/// tekrarı GCM'de güvenliği bozduğu için sayaç ya da sabit değer kullanılmamalı.
/// </remarks>
public sealed class AesGcmFieldEncryptor : IFieldEncryptor
{
    public const string KeySetting = "EVIDENCE_ENCRYPTION_KEY";

    /// <summary>Şu an tek anahtar var. Anahtar değiştirilirken eski sürümler de buraya eklenecek.</summary>
    public const int CurrentKeyVersion = 1;

    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesGcmFieldEncryptor(byte[] key)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException("AES-256 için anahtar 32 bayt olmalı.", nameof(key));
        }

        _key = key;
    }

    /// <summary>Anahtarı ortam değişkeninden / user-secrets'tan okur (Base64, 32 bayt).</summary>
    public static AesGcmFieldEncryptor FromConfiguration(IConfiguration configuration)
    {
        var value = configuration[KeySetting];
        byte[] key;
        try
        {
            key = string.IsNullOrWhiteSpace(value) ? [] : Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            key = [];
        }

        if (key.Length != 32)
        {
            // Koda gömülü yedek anahtar yok; eksik ya da hatalı anahtarla şifreli veri yazılmasın.
            throw new InvalidOperationException(
                $"{KeySetting} tanımlı değil ya da 32 baytlık Base64 değil. Ortam değişkeni veya " +
                "dotnet user-secrets ile ayarlayın (bkz. README).");
        }

        return new AesGcmFieldEncryptor(key);
    }

    public EncryptedData Encrypt(ReadOnlySpan<byte> plaintext, string context)
    {
        var output = new byte[NonceSize + TagSize + plaintext.Length];
        var nonce = output.AsSpan(0, NonceSize);
        var tag = output.AsSpan(NonceSize, TagSize);
        var cipher = output.AsSpan(NonceSize + TagSize);

        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, cipher, tag, Encoding.UTF8.GetBytes(context));

        return new EncryptedData(output, CurrentKeyVersion);
    }

    public byte[] Decrypt(byte[] ciphertext, int keyVersion, string context)
    {
        if (keyVersion != CurrentKeyVersion)
        {
            throw new CryptographicException($"Bilinmeyen anahtar sürümü: {keyVersion}.");
        }

        if (ciphertext.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Şifreli veri eksik.");
        }

        var input = ciphertext.AsSpan();
        var plaintext = new byte[ciphertext.Length - NonceSize - TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(
            input[..NonceSize],
            input[(NonceSize + TagSize)..],
            input.Slice(NonceSize, TagSize),
            plaintext,
            Encoding.UTF8.GetBytes(context));

        return plaintext;
    }
}
