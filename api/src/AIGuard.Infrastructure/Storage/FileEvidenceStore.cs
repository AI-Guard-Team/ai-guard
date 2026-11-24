using System.Text.RegularExpressions;
using AIGuard.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace AIGuard.Infrastructure.Storage;

/// <summary>
/// Kanıt karelerini şifreleyip dosya sistemine yazar (K-26). Docker'da bu klasör bir volume'a
/// bağlanmalı; yoksa container yeniden oluşturulunca kareler kaybolur.
/// </summary>
public sealed partial class FileEvidenceStore(string root, IFieldEncryptor encryptor) : IEvidenceStore
{
    public const string PathSetting = "EVIDENCE_STORAGE_PATH";

    public static FileEvidenceStore FromConfiguration(IConfiguration configuration, IFieldEncryptor encryptor)
    {
        var path = configuration[PathSetting];
        return new FileEvidenceStore(
            string.IsNullOrWhiteSpace(path) ? Path.Combine(AppContext.BaseDirectory, "data", "evidence") : path,
            encryptor);
    }

    /// <summary>Şifreli dosyayı adına bağlayan bağlam; dosyalar birbirinin yerine konamaz.</summary>
    public static string Context(string storageKey) => $"evidence:{storageKey}";

    public async Task<StoredEvidence> SaveAsync(byte[] image, CancellationToken ct = default)
    {
        Directory.CreateDirectory(root);

        // Ad sunucuda üretiliyor; kullanıcı girdisi yola hiç karışmıyor.
        var key = $"{Guid.NewGuid():N}.bin";
        var encrypted = encryptor.Encrypt(image, Context(key));

        await using var file = new FileStream(Path.Combine(root, key), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.WriteAsync(encrypted.Ciphertext, ct);
        return new StoredEvidence(key, encrypted.KeyVersion);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        File.Delete(PathOf(storageKey));
        return Task.CompletedTask;
    }

    public async Task<byte[]?> ReadAsync(string storageKey, int keyVersion, CancellationToken ct = default)
    {
        var path = PathOf(storageKey);
        if (!File.Exists(path))
        {
            return null;
        }

        var ciphertext = await File.ReadAllBytesAsync(path, ct);

        // Bağlam dosya adını içerdiği için başka bir kaydın dosyası bu ada taşınsa bile çözülemez.
        return encryptor.Decrypt(ciphertext, keyVersion, Context(storageKey));
    }

    // Veritabanından gelen ad bile olsa biçimi kontrol ediliyor: "../" gibi bir değer klasör dışına çıkamaz.
    private string PathOf(string storageKey) =>
        KeyPattern().IsMatch(storageKey)
            ? Path.Combine(root, storageKey)
            : throw new ArgumentException("Geçersiz depo anahtarı.", nameof(storageKey));

    [GeneratedRegex("^[0-9a-f]{32}\\.bin$")]
    private static partial Regex KeyPattern();
}
