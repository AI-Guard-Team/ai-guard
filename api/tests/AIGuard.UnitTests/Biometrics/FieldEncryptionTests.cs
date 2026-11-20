using System.Security.Cryptography;
using AIGuard.Application.Biometrics;
using AIGuard.Infrastructure.Security;
using AIGuard.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;

namespace AIGuard.UnitTests.Biometrics;

public class FieldEncryptionTests
{
    private static readonly AesGcmFieldEncryptor Encryptor = new(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Encrypted_vector_round_trips_and_does_not_contain_the_plaintext()
    {
        var vector = Enumerable.Range(0, FaceVector.Dimension).Select(i => i / 512f).ToArray();
        var plain = FaceVector.ToBytes(vector);

        var first = Encryptor.Encrypt(plain, "face-template:a");
        var second = Encryptor.Encrypt(plain, "face-template:a");

        Assert.Equal(vector, FaceVector.FromBytes(Encryptor.Decrypt(first.Ciphertext, first.KeyVersion, "face-template:a")));
        Assert.NotEqual(first.Ciphertext, second.Ciphertext); // her seferinde yeni nonce
        Assert.False(first.Ciphertext.AsSpan().IndexOf(plain.AsSpan(0, 64)) >= 0);
    }

    [Fact]
    public void Tampered_data_wrong_context_or_unknown_key_version_is_rejected()
    {
        var encrypted = Encryptor.Encrypt([1, 2, 3, 4], "face-template:a");
        var tampered = (byte[])encrypted.Ciphertext.Clone();
        tampered[^1] ^= 1;

        // Başka bir adayın satırına kopyalanan şifreli vektör açılamamalı.
        Assert.ThrowsAny<CryptographicException>(() => Encryptor.Decrypt(encrypted.Ciphertext, 1, "face-template:b"));
        Assert.ThrowsAny<CryptographicException>(() => Encryptor.Decrypt(tampered, 1, "face-template:a"));
        Assert.ThrowsAny<CryptographicException>(() => Encryptor.Decrypt(encrypted.Ciphertext, 2, "face-template:a"));
        Assert.ThrowsAny<CryptographicException>(() => Encryptor.Decrypt([1, 2, 3], 1, "face-template:a"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("kisa")]
    [InlineData("change_me_base64_32_bytes")] // .env.example'daki yer tutucu
    public void Missing_or_invalid_key_stops_instead_of_using_a_default(string? key)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AesGcmFieldEncryptor.KeySetting] = key })
            .Build();

        Assert.Throws<InvalidOperationException>(() => AesGcmFieldEncryptor.FromConfiguration(configuration));
    }

    [Fact]
    public void Face_vector_rejects_wrong_size_non_finite_or_unnormalized_values()
    {
        var ok = new float[FaceVector.Dimension];
        ok[0] = 0.6f;
        ok[1] = 0.8f; // uzunluk 1
        var nan = (float[])ok.Clone();
        nan[3] = float.NaN;
        var unnormalized = Enumerable.Repeat(0.1f, FaceVector.Dimension).ToArray(); // uzunluk ~2,26

        Assert.True(FaceVector.IsValid(ok));
        Assert.False(FaceVector.IsValid(nan));
        Assert.False(FaceVector.IsValid(unnormalized));
        Assert.False(FaceVector.IsValid(new float[FaceVector.Dimension])); // sıfır vektör
        Assert.False(FaceVector.IsValid(new float[128]));
        Assert.False(FaceVector.IsValid(null));
        Assert.Throws<ArgumentException>(() => FaceVector.FromBytes(new byte[10]));
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, true)]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, true)]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, true)]  // RIFF....WEBP
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x41, 0x56, 0x45 }, false)] // RIFF....WAVE
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38 }, false)] // GIF
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67 }, false)] // <svg
    public void Only_jpeg_png_and_webp_are_accepted(byte[] data, bool expected)
    {
        Assert.Equal(expected, ImageFile.IsAcceptable(data, ImageFile.MaxFrameBytes));
    }

    [Fact]
    public async Task Evidence_file_is_encrypted_on_disk_and_keys_cannot_escape_the_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"aiguard-evidence-{Guid.NewGuid():N}");
        var store = new FileEvidenceStore(root, Encryptor);
        byte[] image = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8];

        try
        {
            var stored = await store.SaveAsync(image);
            var onDisk = await File.ReadAllBytesAsync(Path.Combine(root, stored.StorageKey));

            Assert.False(onDisk.AsSpan().IndexOf(image) >= 0);
            Assert.Equal(image, Encryptor.Decrypt(onDisk, stored.KeyVersion, FileEvidenceStore.Context(stored.StorageKey)));

            await store.DeleteAsync(stored.StorageKey);
            Assert.False(File.Exists(Path.Combine(root, stored.StorageKey)));
            await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteAsync("../appsettings.json"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
