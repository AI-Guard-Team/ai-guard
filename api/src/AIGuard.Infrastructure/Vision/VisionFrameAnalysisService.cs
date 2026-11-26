using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Monitoring;
using Microsoft.Extensions.Configuration;

namespace AIGuard.Infrastructure.Vision;

/// <summary>Görüntü servisinin kare analizi ucuna HTTP istemcisi (docs/contracts.md 5. bölüm, D4).</summary>
/// <remarks>
/// POST /internal/analyze/frame { sessionId, frame, ts, tasks[], referenceVector? }
///   → { sessionId, ts, faceCount, yaw, pitch, gazeAway, phone, identity, evidenceFrame, processingMs }
///
/// Yanıtlar VisionFaceService'teki gibi ikiye ayrılıyor:
/// - Adayın karesi: 422 INVALID_IMAGE → <see cref="FrameAnalysisStatus.InvalidImage"/> (kare atlanıyor).
/// - Servis ya da sözleşme sorunu: bağlantı hatası, zaman aşımı, 401, 503, VALIDATION_ERROR, bilinmeyen kod, eksik
///   ya da aralık dışı alan → <see cref="FrameAnalysisStatus.Unavailable"/> (MONITORING_GAP).
/// Günlüğü çağıran yazıyor, boşluk başına bir kez (CameraMonitor); burada görüntü, vektör ve ölçüm hiç yazılmıyor.
/// </remarks>
public sealed class VisionFrameAnalysisService(HttpClient http) : IFrameAnalysisService
{
    private const string FramePath = "internal/analyze/frame";
    private const string KeyHeader = "X-Service-Key";

    /// <summary>
    /// Adres ve anahtar VisionFaceService ile aynı ayarlardan. Anahtar yoksa istek gönderilmiyor (kare anahtarsız
    /// gitmesin), sonuç NOT_CONFIGURED boşluğu; kayıt fotoğrafı uçlarının aksine kare ucu 500 vermiyor.
    /// </summary>
    public static void Configure(HttpClient client, IConfiguration configuration, CameraMonitorOptions options)
    {
        var url = configuration[VisionFaceService.UrlSetting];
        client.BaseAddress = new Uri((string.IsNullOrWhiteSpace(url) ? VisionFaceService.DefaultUrl : url).TrimEnd('/') + "/");

        var key = configuration[VisionFaceService.KeySetting];
        if (!string.IsNullOrWhiteSpace(key))
        {
            client.DefaultRequestHeaders.Add(KeyHeader, key);
        }

        // Kısa: tarayıcı bu kareyi beklemeden yenisini göndermiyor (K-30, K-45).
        client.Timeout = TimeSpan.FromSeconds(options.VisionTimeoutSeconds);
    }

    public async Task<FrameAnalysisResult> AnalyzeAsync(
        Guid sessionId, byte[] frame, DateTimeOffset ts, bool phone, float[]? referenceVector, CancellationToken ct = default)
    {
        if (!http.DefaultRequestHeaders.Contains(KeyHeader))
        {
            return FrameAnalysisResult.Unavailable(null, "NOT_CONFIGURED");
        }

        List<string> tasks = ["face", "pose"];
        if (phone)
        {
            tasks.Add("phone");
        }

        var identity = referenceVector is not null;
        if (identity)
        {
            tasks.Add("identity");
        }

        // referenceVector yalnızca identity istendiğinde gönderiliyor (S-21).
        var request = new Dictionary<string, object?>
        {
            ["sessionId"] = sessionId,
            ["frame"] = Convert.ToBase64String(frame),
            ["ts"] = ts.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            ["tasks"] = tasks,
        };
        if (identity)
        {
            request["referenceVector"] = referenceVector;
        }

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(FramePath, request, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return FrameAnalysisResult.Unavailable(null, "TIMEOUT");
        }
        catch (HttpRequestException)
        {
            return FrameAnalysisResult.Unavailable(null, "UNREACHABLE");
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                var code = await ReadErrorCodeAsync(response, ct);
                return code == FaceIssue.InvalidImage
                    ? FrameAnalysisResult.InvalidImage()
                    : FrameAnalysisResult.Unavailable(status, code);
            }

            if (!response.IsSuccessStatusCode)
            {
                // 401: anahtar uyuşmuyor; 503 MODELS_NOT_READY: modeller yükleniyor.
                return FrameAnalysisResult.Unavailable(status, await ReadErrorCodeAsync(response, ct));
            }

            FrameResponse? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<FrameResponse>(ct);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                return FrameAnalysisResult.Unavailable(status, "UNREADABLE_RESPONSE");
            }

            var measurement = body is null ? null : ToMeasurement(body, sessionId, phone, identity);
            return measurement is null
                ? FrameAnalysisResult.Unavailable(status, "INVALID_RESPONSE")
                : FrameAnalysisResult.Ok(measurement);
        }
    }

    /// <summary>Yanıtı sözleşmeye göre doğrular; uymuyorsa <c>null</c> (sözleşme kayması, boşluk sayılıyor).</summary>
    private static FrameMeasurement? ToMeasurement(FrameResponse b, Guid sessionId, bool phoneRequested, bool identityRequested)
    {
        if (b.SessionId != sessionId || b.FaceCount is not { } faces || faces < 0)
        {
            return null;
        }

        // Yüz varsa kafa yönü dolu olmalı; yüz yoksa üçü de null (sözleşme), değerlere bakılmıyor.
        bool? gazeAway = null;
        double? yaw = null, pitch = null;
        if (faces > 0)
        {
            if (b.GazeAway is not { } gaze || !IsFinite(b.Yaw) || !IsFinite(b.Pitch))
            {
                return null;
            }

            (gazeAway, yaw, pitch) = (gaze, b.Yaw, b.Pitch);
        }

        PhoneReading? phone = null;
        if (phoneRequested)
        {
            if (b.Phone is not { Detected: { } detected, Confidence: { } confidence } || !double.IsFinite(confidence) || confidence is < 0 or > 1)
            {
                return null;
            }

            phone = new PhoneReading(detected, confidence);
        }

        // Yüz yoksa identity null; yüz varken boş gelmesi sözleşmeye aykırı.
        IdentityReading? identity = null;
        if (identityRequested && b.Identity is { } id)
        {
            if (id.Matched is not { } matched || id.Similarity is not { } similarity || !double.IsFinite(similarity) || similarity is < -1 or > 1)
            {
                return null;
            }

            identity = new IdentityReading(matched, similarity);
        }
        else if (identityRequested && faces > 0)
        {
            return null;
        }

        byte[]? evidence = null;
        if (!string.IsNullOrEmpty(b.EvidenceFrame))
        {
            try
            {
                evidence = Convert.FromBase64String(b.EvidenceFrame);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        return new FrameMeasurement(faces, yaw, pitch, gazeAway, phone, identity, evidence);
    }

    private static bool IsFinite(double? value) => value is { } v && double.IsFinite(v);

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

    private sealed record FrameResponse(
        Guid? SessionId,
        int? FaceCount,
        double? Yaw,
        double? Pitch,
        bool? GazeAway,
        PhoneBody? Phone,
        IdentityBody? Identity,
        string? EvidenceFrame);

    private sealed record PhoneBody(bool? Detected, double? Confidence);

    private sealed record IdentityBody(double? Similarity, bool? Matched);

    private sealed record ErrorResponse(ErrorDetail? Error);

    private sealed record ErrorDetail(string? Code, string? Message);
}
