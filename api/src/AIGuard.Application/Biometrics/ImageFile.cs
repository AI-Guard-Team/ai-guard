namespace AIGuard.Application.Biometrics;

/// <summary>Yüklenen görüntülerin boyut ve biçim kontrolü.</summary>
/// <remarks>
/// İstemcinin bildirdiği Content-Type'a güvenilmiyor; dosyanın ilk baytlarına bakılıyor.
/// Yalnızca JPEG, PNG ve WebP kabul ediliyor (S2'deki fotoğraf ekranı ve docs/contracts.md);
/// görüntü servisine başka bir şey gitmesin.
/// </remarks>
public static class ImageFile
{
    public const int MaxPhotoBytes = 5 * 1024 * 1024;
    public const int MaxFrameBytes = 1024 * 1024;

    /// <summary>Doğrulamada kare sayısı (docs/contracts.md 4. bölüm: 3–5 kare).</summary>
    public const int MinFrames = 3;
    public const int MaxFrames = 5;

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] WebP = "WEBP"u8.ToArray();

    public static bool IsSupportedImage(ReadOnlySpan<byte> data) =>
        data.StartsWith(Jpeg)
        || data.StartsWith(Png)
        // WebP: "RIFF" + 4 bayt uzunluk + "WEBP"
        || (data.Length >= 12 && data.StartsWith(Riff) && data.Slice(8, 4).SequenceEqual(WebP));

    public static bool IsAcceptable(byte[]? data, int maxBytes) =>
        data is { Length: > 0 } && data.Length <= maxBytes && IsSupportedImage(data);
}
