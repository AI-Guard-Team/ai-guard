namespace AIGuard.Application.Abstractions;

public sealed record StoredEvidence(string StorageKey, int KeyVersion);

/// <summary>Kanıt karelerinin şifreli dosya deposu (K-26).</summary>
/// <remarks>
/// Dosya adını depo üretir; kullanıcı girdisinden yol oluşturulmaz. Okuma değerlendirici ekranı için
/// (A4, kısa ömürlü imzalı bağlantıyla); saklama süresi dolanların silinmesi A5'te eklenecek.
/// </remarks>
public interface IEvidenceStore
{
    Task<StoredEvidence> SaveAsync(byte[] image, CancellationToken ct = default);

    /// <summary>Kaydı veritabanına yazılamayan dosyayı geri almak için. Dosya yoksa sessizce geçer.</summary>
    Task DeleteAsync(string storageKey, CancellationToken ct = default);

    /// <summary>Kareyi çözüp döner; dosya silinmişse (saklama süresi doldu) <c>null</c>.</summary>
    Task<byte[]?> ReadAsync(string storageKey, int keyVersion, CancellationToken ct = default);
}
