using System.Net;
using System.Text;
using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Monitoring;
using AIGuard.Infrastructure.Vision;
using Microsoft.Extensions.Configuration;

namespace AIGuard.UnitTests.Monitoring;

/// <summary>
/// /internal/analyze/frame istemcisinin docs/contracts.md 5. bölümdeki yanıtlara tepkisi. INVALID_IMAGE adayın
/// karesi (atlanıyor); servis ve sözleşme sorunları Unavailable (MONITORING_GAP).
/// </summary>
public class VisionFrameAnalysisServiceTests
{
    private static readonly Guid Session = Guid.Parse("8f3c0000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Ts = new(2026, 11, 24, 10, 15, 30, 250, TimeSpan.Zero);

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        public JsonElement? Body { get; private set; }

        public string? Key { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            Key = request.Headers.TryGetValues("X-Service-Key", out var keys) ? keys.Single() : null;
            if (request.Content is not null)
            {
                Body = JsonDocument.Parse(await request.Content.ReadAsStringAsync(ct)).RootElement.Clone();
            }

            return await respond(request, ct);
        }
    }

    private static (VisionFrameAnalysisService Service, StubHandler Handler) Create(
        HttpStatusCode status, string body, string? key = "test-key", double timeoutSeconds = 3) =>
        Create((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }),
            key, timeoutSeconds);

    private static (VisionFrameAnalysisService Service, StubHandler Handler) Create(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond, string? key = "test-key", double timeoutSeconds = 3)
    {
        var handler = new StubHandler(respond);
        var client = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [VisionFaceService.KeySetting] = key,
                [VisionFaceService.UrlSetting] = "http://vision:8000",
            })
            .Build();
        VisionFrameAnalysisService.Configure(client, configuration, new CameraMonitorOptions { VisionTimeoutSeconds = timeoutSeconds });
        return (new VisionFrameAnalysisService(client), handler);
    }

    private static string Error(string code) => JsonSerializer.Serialize(new { error = new { code, message = "x" } });

    private static string Response(
        int faceCount = 1, double? yaw = 34.2, double? pitch = -5.1, bool? gazeAway = true,
        object? phone = null, object? identity = null, string? evidenceFrame = null, string? sessionId = null) =>
        JsonSerializer.Serialize(new
        {
            sessionId = sessionId ?? Session.ToString(),
            ts = "2026-11-24T10:15:30.250Z",
            faceCount,
            yaw = faceCount > 0 ? yaw : null,
            pitch = faceCount > 0 ? pitch : null,
            gazeAway = faceCount > 0 ? gazeAway : null,
            phone,
            identity,
            evidenceFrame,
            processingMs = 12,
        });

    private static float[] UnitVector()
    {
        var v = new float[512];
        v[0] = 1;
        return v;
    }

    private static Task<FrameAnalysisResult> Analyze(VisionFrameAnalysisService service, bool phone = false, bool identity = false) =>
        service.AnalyzeAsync(Session, [0xFF, 0xD8, 0xFF], Ts, phone, identity ? UnitVector() : null);

    // ---------- istek ----------

    [Fact]
    public async Task Face_and_pose_only_request_has_no_reference_vector()
    {
        var (service, handler) = Create(HttpStatusCode.OK, Response());

        await Analyze(service);

        var body = handler.Body!.Value;
        Assert.Equal("test-key", handler.Key);
        Assert.Equal(Session.ToString(), body.GetProperty("sessionId").GetString());
        Assert.Equal("2026-11-24T10:15:30.250Z", body.GetProperty("ts").GetString());
        Assert.Equal(Convert.ToBase64String([0xFF, 0xD8, 0xFF]), body.GetProperty("frame").GetString());
        Assert.Equal(["face", "pose"], body.GetProperty("tasks").EnumerateArray().Select(t => t.GetString()));
        Assert.False(body.TryGetProperty("referenceVector", out _)); // S-21
    }

    [Fact]
    public async Task Phone_and_identity_are_requested_with_the_vector()
    {
        var (service, handler) = Create(HttpStatusCode.OK, Response(
            phone: new { detected = false, confidence = 0.08 }, identity: new { similarity = 0.58, matched = true }));

        var result = await Analyze(service, phone: true, identity: true);

        var body = handler.Body!.Value;
        Assert.Equal(["face", "pose", "phone", "identity"], body.GetProperty("tasks").EnumerateArray().Select(t => t.GetString()));
        Assert.Equal(512, body.GetProperty("referenceVector").GetArrayLength());

        Assert.Equal(FrameAnalysisStatus.Ok, result.Status);
        var m = result.Measurement!;
        Assert.Equal(1, m.FaceCount);
        Assert.Equal(34.2, m.Yaw);
        Assert.True(m.GazeAway);
        Assert.Equal(new PhoneReading(false, 0.08), m.Phone);
        Assert.Equal(new IdentityReading(true, 0.58), m.Identity);
        Assert.Null(m.EvidenceFrame);
    }

    [Fact]
    public async Task Missing_service_key_sends_nothing()
    {
        var (service, handler) = Create(HttpStatusCode.OK, Response(), key: null);

        var result = await Analyze(service);

        Assert.Equal(0, handler.Requests);
        Assert.Equal(FrameAnalysisStatus.Unavailable, result.Status);
        Assert.Equal("NOT_CONFIGURED", result.Code);
    }

    // ---------- yanıt ----------

    [Fact]
    public async Task No_face_returns_null_pose_and_identity()
    {
        var (service, _) = Create(HttpStatusCode.OK, Response(faceCount: 0, phone: new { detected = true, confidence = 0.9 }, evidenceFrame: "AQID"));

        var result = await Analyze(service, phone: true, identity: true);

        var m = result.Measurement!;
        Assert.Equal(0, m.FaceCount);
        Assert.Null(m.GazeAway);
        Assert.Null(m.Yaw);
        Assert.Null(m.Identity);
        Assert.Equal(new PhoneReading(true, 0.9), m.Phone);
        Assert.Equal([1, 2, 3], m.EvidenceFrame);
    }

    [Fact]
    public async Task Invalid_image_is_the_candidates_frame_not_a_service_problem()
    {
        var (service, _) = Create(HttpStatusCode.UnprocessableEntity, Error(FaceIssue.InvalidImage));

        Assert.Equal(FrameAnalysisStatus.InvalidImage, (await Analyze(service)).Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "NO_FACE")]
    [InlineData(HttpStatusCode.Unauthorized, "UNAUTHORIZED")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "MODELS_NOT_READY")]
    [InlineData(HttpStatusCode.InternalServerError, "INTERNAL_ERROR")]
    public async Task Service_errors_are_unavailable_with_status_and_code(HttpStatusCode status, string code)
    {
        var (service, _) = Create(status, Error(code));

        var result = await Analyze(service);

        Assert.Equal(FrameAnalysisStatus.Unavailable, result.Status);
        Assert.Equal((int)status, result.HttpStatus);
        Assert.Equal(code, result.Code);
    }

    [Fact]
    public async Task Non_contract_error_body_is_unavailable_without_code()
    {
        var (service, _) = Create(HttpStatusCode.NotFound, "{\"detail\":\"Not Found\"}");

        var result = await Analyze(service);

        Assert.Equal(FrameAnalysisStatus.Unavailable, result.Status);
        Assert.Equal(404, result.HttpStatus);
        Assert.Null(result.Code);
    }

    [Fact]
    public async Task Connection_failure_is_unreachable()
    {
        var (service, _) = Create((_, _) => throw new HttpRequestException("bağlantı reddedildi"));

        var result = await Analyze(service);

        Assert.Equal(FrameAnalysisStatus.Unavailable, result.Status);
        Assert.Null(result.HttpStatus);
        Assert.Equal("UNREACHABLE", result.Code);
    }

    [Fact]
    public async Task Slow_service_times_out()
    {
        var (service, _) = Create(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, timeoutSeconds: 0.1);

        var result = await Analyze(service);

        Assert.Equal(FrameAnalysisStatus.Unavailable, result.Status);
        Assert.Equal("TIMEOUT", result.Code);
    }

    [Fact]
    public async Task Cancelled_request_is_not_a_gap()
    {
        var (service, _) = Create(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.AnalyzeAsync(Session, [1], Ts, false, null, cts.Token));
    }

    public static TheoryData<string, bool, bool> ContractViolations() => new()
    {
        // Yüz var ama kafa yönü yok.
        { Response(gazeAway: null), false, false },
        { Response(yaw: null), false, false },
        // Telefon istendi ama alan yok ya da aralık dışı.
        { Response(), true, false },
        { Response(phone: new { detected = true, confidence = 1.5 }), true, false },
        // Kimlik istendi, yüz var ama identity yok; benzerlik aralık dışı.
        { Response(), false, true },
        { Response(identity: new { similarity = 1.2, matched = true }), false, true },
        { Response(identity: new { similarity = 0.4 }), false, true },
        // Başka oturumun yanıtı, bozuk kanıt karesi, negatif yüz sayısı.
        { Response(sessionId: Guid.NewGuid().ToString()), false, false },
        { Response(evidenceFrame: "%%%"), false, false },
        { Response(faceCount: -1), false, false },
        { "{}", false, false },
    };

    [Theory]
    [MemberData(nameof(ContractViolations))]
    public async Task Response_outside_the_contract_is_unavailable(string body, bool phone, bool identity)
    {
        var (service, _) = Create(HttpStatusCode.OK, body);

        var result = await Analyze(service, phone, identity);

        Assert.Equal(FrameAnalysisStatus.Unavailable, result.Status);
        Assert.Equal("INVALID_RESPONSE", result.Code);
    }

    [Fact]
    public async Task Unreadable_response_is_unavailable()
    {
        var (service, _) = Create(HttpStatusCode.OK, "not json");

        var result = await Analyze(service);

        Assert.Equal("UNREADABLE_RESPONSE", result.Code);
    }
}
