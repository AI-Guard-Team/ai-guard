using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Biometrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIGuard.Infrastructure.Vision;

/// <summary>Görüntü servisinin yüz uçlarına HTTP istemcisi (docs/contracts.md 3. ve 4. bölüm).</summary>
/// <remarks>
/// POST /internal/face/enroll  { image }                     → { vector, faceBox, qualityScore }
/// POST /internal/face/verify  { frames[], referenceVector } → { livenessScore, similarity, passed, reason, framesUsed }
/// Görüntüler Base64; istekler X-Service-Key başlığıyla. Hata gövdesi { error: { code, message } }.
///
/// Yanıtlar iki gruba ayrılıyor:
/// - Adayla ilgili sonuç: kayıtta 422 + <see cref="FaceIssue.EnrollRejections"/>, doğrulamada 200 (passed=false)
///   ya da 422 INVALID_IMAGE. Bunlar adaya nedeniyle dönüyor, doğrulamada deneme olarak kaydediliyor.
/// - Servis ya da sözleşme sorunu: bağlantı hatası, 401, 503, VALIDATION_ERROR, bilinmeyen kod, eksik veya
///   aralık dışı alan. Bunlar <see cref="FaceServiceUnavailableException"/>; adayın hanesine yazılmıyor.
/// Günlüğe yalnızca durum kodu ve hata kodu yazılıyor; görüntü, vektör ve skor yazılmıyor (KVKK).
/// </remarks>
public sealed class VisionFaceService(HttpClient http, ILogger<VisionFaceService> logger) : IFaceService
{
    public const string UrlSetting = "VISION_SERVICE_URL";
    public const string KeySetting = "VISION_SERVICE_KEY";
    public const string DefaultUrl = "http://localhost:8000";

    private const string EnrollPath = "internal/face/enroll";
    private const string VerifyPath = "internal/face/verify";

    // Doğrulama yanıtında sözleşmenin tanımladığı nedenler; başka bir değer sözleşme kayması demek.
    private static readonly HashSet<string> VerifyReasons =
        [FaceIssue.LivenessFailed, FaceIssue.IdentityMismatch, FaceIssue.NoFace, FaceIssue.MultipleFaces];

    public async Task<FaceEnrollResult> EnrollAsync(byte[] image, CancellationToken ct = default)
    {
        using var response = await SendAsync(EnrollPath, new { image = Convert.ToBase64String(image) }, ct);

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var code = await ReadErrorCodeAsync(response, ct);
            if (code is not null && FaceIssue.EnrollRejections.Contains(code))
            {
                return FaceEnrollResult.Rejection(code);
            }

            throw Unavailable(EnrollPath, response.StatusCode, code);
        }

        var body = await ReadAsync<EnrollResponse>(EnrollPath, response, ct);
        if (!FaceVector.IsValid(body.Vector))
        {
            // 512 değil, sonlu değil ya da birim uzunlukta değil: model ya da sözleşme değişmiş olabilir.
            throw Unavailable(EnrollPath, response.StatusCode, "INVALID_VECTOR");
        }

        return FaceEnrollResult.Ok(body.Vector!);
    }

    public async Task<FaceVerifyResult> VerifyAsync(IReadOnlyList<byte[]> frames, float[] referenceVector, CancellationToken ct = default)
    {
        var request = new { frames = frames.Select(Convert.ToBase64String).ToArray(), referenceVector };
        using var response = await SendAsync(VerifyPath, request, ct);

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            // Yalnızca kare çözülemediğinde adayın denemesi sayılıyor (bozuk kamera görüntüsü).
            // VALIDATION_ERROR gibi diğer kodlar bizim isteğimizin sözleşmeye uymadığını gösteriyor.
            var code = await ReadErrorCodeAsync(response, ct);
            if (code == FaceIssue.InvalidImage)
            {
                return new FaceVerifyResult(0, 0, false, FaceIssue.InvalidImage);
            }

            throw Unavailable(VerifyPath, response.StatusCode, code);
        }

        var body = await ReadAsync<VerifyResponse>(VerifyPath, response, ct);
        if (body.LivenessScore is not { } liveness || !double.IsFinite(liveness) || liveness is < 0 or > 1
            || body.Similarity is not { } similarity || !double.IsFinite(similarity) || similarity is < -1 or > 1
            || body.Passed is not { } passed)
        {
            throw Unavailable(VerifyPath, response.StatusCode, "INVALID_RESPONSE");
        }

        if (passed)
        {
            return new FaceVerifyResult(liveness, similarity, true);
        }

        // Nedeni olmayan ya da tanınmayan ret de ret; aday yine engelleniyor, yalnızca iz bırakılıyor.
        if (body.Reason is null || !VerifyReasons.Contains(body.Reason))
        {
            logger.LogWarning("Görüntü servisi tanınmayan bir doğrulama nedeni döndü: {Reason}", body.Reason ?? "(boş)");
        }

        return new FaceVerifyResult(liveness, similarity, false, body.Reason);
    }

    /// <summary>HttpClient ayarları; adres ve anahtar ilk kullanımda okunuyor.</summary>
    public static void Configure(HttpClient client, IConfiguration configuration)
    {
        var key = configuration[KeySetting];
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                $"{KeySetting} tanımlı değil. Ortam değişkeni veya dotnet user-secrets ile ayarlayın (bkz. README).");
        }

        var url = configuration[UrlSetting];
        client.BaseAddress = new Uri((string.IsNullOrWhiteSpace(url) ? DefaultUrl : url).TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Add("X-Service-Key", key);

        // Doğrulama ekranı bu süre kadar bekleyebilir; servis yanıt vermezse aday tekrar dener.
        client.Timeout = TimeSpan.FromSeconds(15);
    }

    private async Task<HttpResponseMessage> SendAsync(string path, object body, CancellationToken ct)
    {
        try
        {
            return await http.PostAsJsonAsync(path, body, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Görüntü servisine ulaşılamadı ({Path}): {Error}", path, ex.GetType().Name);
            throw new FaceServiceUnavailableException("Görüntü servisine ulaşılamadı.", ex);
        }
    }

    private async Task<T> ReadAsync<T>(string path, HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            // 401: servis anahtarı uyuşmuyor; 503 MODELS_NOT_READY: modeller yükleniyor.
            throw Unavailable(path, response.StatusCode, await ReadErrorCodeAsync(response, ct));
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(ct)
                ?? throw Unavailable(path, response.StatusCode, "EMPTY_RESPONSE");
        }
        catch (JsonException)
        {
            throw Unavailable(path, response.StatusCode, "UNREADABLE_RESPONSE");
        }
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(ct);
            return body?.Error?.Code;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            // Sözleşme dışı gövde (ör. FastAPI'nin varsayılan hata biçimi); kod yok sayılıyor.
            return null;
        }
    }

    private FaceServiceUnavailableException Unavailable(string path, HttpStatusCode status, string? code)
    {
        logger.LogWarning("Görüntü servisi kullanılamadı ({Path}): HTTP {Status}, kod {Code}", path, (int)status, code ?? "(yok)");
        return new FaceServiceUnavailableException($"Görüntü servisi {(int)status} {code} döndü.");
    }

    private sealed record EnrollResponse(float[]? Vector);

    private sealed record VerifyResponse(double? LivenessScore, double? Similarity, bool? Passed, string? Reason);

    private sealed record ErrorResponse(ErrorDetail? Error);

    private sealed record ErrorDetail(string? Code, string? Message);
}
