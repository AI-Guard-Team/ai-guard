namespace AIGuard.Api.Controllers;

internal static class FormFiles
{
    /// <summary>Dosyayı belleğe okur; boşsa ya da sınırı aşıyorsa null döner.</summary>
    /// <remarks>Görüntüler diske yazılmıyor (geçici dosya da yok); sınırlar küçük olduğu için bellekte tutmak yeterli.</remarks>
    public static async Task<byte[]?> ReadAsync(IFormFile? file, int maxBytes, CancellationToken ct)
    {
        if (file is null || file.Length == 0 || file.Length > maxBytes)
        {
            return null;
        }

        var buffer = new byte[file.Length];
        await using var stream = file.OpenReadStream();
        await stream.ReadExactlyAsync(buffer, ct);
        return buffer;
    }
}
