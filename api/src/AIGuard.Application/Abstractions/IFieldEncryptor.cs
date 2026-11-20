namespace AIGuard.Application.Abstractions;

/// <param name="Ciphertext">Nonce, doğrulama etiketi ve şifreli veri tek dizide.</param>
/// <param name="KeyVersion">Şifrelemede kullanılan anahtarın sürümü; kayıtla birlikte saklanmalı.</param>
public sealed record EncryptedData(byte[] Ciphertext, int KeyVersion);

/// <summary>
/// Veritabanına veya dosyaya yazılan hassas verinin (yüz vektörü, kanıt karesi) şifrelenmesi.
/// </summary>
/// <remarks>
/// <paramref name="context"/> şifreli verinin ait olduğu kaydı belirtir (ör. "face-template:{userId}").
/// Aynı bağlamla açılmayan veri reddedilir; böylece bir adayın şifreli vektörü başka bir adayın
/// satırına kopyalansa bile kullanılamaz.
/// </remarks>
public interface IFieldEncryptor
{
    EncryptedData Encrypt(ReadOnlySpan<byte> plaintext, string context);

    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Veri değiştirilmiş, bağlam farklı ya da anahtar sürümü bilinmiyorsa.
    /// </exception>
    byte[] Decrypt(byte[] ciphertext, int keyVersion, string context);
}
