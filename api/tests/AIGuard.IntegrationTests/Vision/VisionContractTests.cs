using System.IO.Compression;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Biometrics;
using AIGuard.Infrastructure.Vision;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIGuard.IntegrationTests.Vision;

/// <summary>
/// A3'ün görüntü servisi istemcisini çalışan gerçek servise karşı sınar (docs/contracts.md 3. ve 4. bölüm).
/// Sahte servisle yazılan testler sözleşmeyi bizim okuduğumuz gibi sınıyor; bunlar D2'nin de aynı şekilde
/// okuduğunu gösteriyor.
/// </summary>
/// <remarks>
/// Varsayılan olarak atlanıyor. Çalıştırmak için (README, "Görüntü servisiyle sözleşme testi"):
///   VISION_CONTRACT_URL = http://localhost:8000
///   VISION_SERVICE_KEY  = vision'ın kullandığı anahtar
///   AIGUARD_TEST_FACE   = (isteğe bağlı) kendi yüzünüzün bulunduğu bir JPEG'in yolu; repoya eklenmez
/// </remarks>
public class VisionContractTests
{
    private static readonly string? Url = Environment.GetEnvironmentVariable("VISION_CONTRACT_URL");
    private static readonly string? Key = Environment.GetEnvironmentVariable(VisionFaceService.KeySetting);
    private static readonly string? FacePath = Environment.GetEnvironmentVariable("AIGUARD_TEST_FACE");

    private static void RequireVision() =>
        Skip.If(string.IsNullOrWhiteSpace(Url) || string.IsNullOrWhiteSpace(Key),
            "VISION_CONTRACT_URL ve VISION_SERVICE_KEY tanımlı değil; gerçek görüntü servisiyle sözleşme testi atlandı.");

    private static VisionFaceService Client(string key)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [VisionFaceService.UrlSetting] = Url,
                [VisionFaceService.KeySetting] = key,
            })
            .Build();
        var http = new HttpClient();
        VisionFaceService.Configure(http, configuration);
        return new VisionFaceService(http, NullLogger<VisionFaceService>.Instance);
    }

    [SkippableFact]
    public async Task Wrong_service_key_is_unavailable_not_a_rejected_photo()
    {
        RequireVision();

        await Assert.ThrowsAsync<FaceServiceUnavailableException>(
            () => Client("yanlis-anahtar").EnrollAsync(BlankImage()));
    }

    [SkippableFact]
    public async Task Photo_without_a_face_is_rejected_with_a_known_reason()
    {
        RequireVision();

        var result = await Client(Key!).EnrollAsync(BlankImage());

        Assert.True(result.Rejected);
        Assert.Contains(result.Issue!, FaceIssue.EnrollRejections);
    }

    [SkippableFact]
    public async Task Frames_without_a_face_fail_verification_with_a_reason()
    {
        RequireVision();
        var reference = new float[FaceVector.Dimension];
        reference[0] = 1;

        var result = await Client(Key!).VerifyAsync([BlankImage(), BlankImage(), BlankImage()], reference);

        Assert.False(result.Passed);
        Assert.NotNull(result.Reason);
    }

    [SkippableFact]
    public async Task Real_face_gives_a_unit_vector_and_matches_itself()
    {
        RequireVision();
        Skip.If(string.IsNullOrWhiteSpace(FacePath) || !File.Exists(FacePath),
            "AIGUARD_TEST_FACE tanımlı değil; yüzlü fotoğraf adımı atlandı.");
        var photo = await File.ReadAllBytesAsync(FacePath!);
        var client = Client(Key!);

        var enrolled = await client.EnrollAsync(photo);
        Assert.False(enrolled.Rejected, $"Fotoğraf reddedildi: {enrolled.Issue}");
        Assert.True(FaceVector.IsValid(enrolled.Vector));

        // Aynı fotoğraf kendi vektörüyle karşılaştırılıyor: benzerlik çok yüksek olmalı. Canlılık sonucu
        // burada sınanmıyor; durağan bir fotoğrafın canlılıkta reddedilmesi beklenen davranış olabilir (D6).
        var verified = await client.VerifyAsync([photo, photo, photo], enrolled.Vector!);
        Assert.True(verified.Similarity > 0.9, $"Benzerlik {verified.Similarity:0.000}");
    }

    /// <summary>Yüz içermeyen 64×64 gri PNG; yüz fotoğrafı repoya eklenmediği için kodla üretiliyor.</summary>
    private static byte[] BlankImage()
    {
        const int size = 64;
        var raw = new byte[size * (size * 3 + 1)]; // her satırın başında filtre baytı (0)
        for (var y = 0; y < size; y++)
        {
            Array.Fill(raw, (byte)128, y * (size * 3 + 1) + 1, size * 3);
        }

        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(raw);
        }

        var header = new byte[13];
        WriteBigEndian(header, 0, size);
        WriteBigEndian(header, 4, size);
        header[8] = 8; // bit derinliği
        header[9] = 2; // RGB

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        var typeAndData = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        var crc = new byte[4];
        WriteBigEndian(crc, 0, (int)Crc32(typeAndData));
        stream.Write(length);
        stream.Write(typeAndData);
        stream.Write(crc);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
