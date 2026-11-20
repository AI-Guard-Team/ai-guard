using System.Net;
using System.Text;
using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Biometrics;
using AIGuard.Infrastructure.Vision;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIGuard.UnitTests.Biometrics;

/// <summary>
/// Görüntü servisi istemcisinin docs/contracts.md'deki yanıtlara tepkisi. Asıl ayrım: adayla ilgili
/// sonuçlar adaya dönüyor, servis veya sözleşme sorunları 503 oluyor ve adayın hanesine yazılmıyor.
/// </summary>
public class VisionFaceServiceTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("bağlantı reddedildi");
    }

    private static VisionFaceService Create(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [VisionFaceService.KeySetting] = "test-key",
                [VisionFaceService.UrlSetting] = "http://vision:8000",
            })
            .Build();
        VisionFaceService.Configure(client, configuration);
        return new VisionFaceService(client, NullLogger<VisionFaceService>.Instance);
    }

    private static (VisionFaceService Service, StubHandler Handler) Create(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        return (Create(handler), handler);
    }

    private static string Error(string code) => JsonSerializer.Serialize(new { error = new { code, message = "x" } });

    private static float[] UnitVector()
    {
        var v = new float[FaceVector.Dimension];
        v[0] = 1;
        return v;
    }

    private static string EnrollJson(float[] vector) =>
        JsonSerializer.Serialize(new { vector, faceBox = new { x = 1, y = 2, width = 100, height = 100 }, qualityScore = 0.9 });

    private static Task<FaceVerifyResult> Verify(VisionFaceService service) =>
        service.VerifyAsync([[1], [2], [3]], UnitVector());

    // ---------- kayıt ----------

    [Fact]
    public async Task Enroll_sends_the_service_key_and_returns_the_vector()
    {
        var (service, handler) = Create(HttpStatusCode.OK, EnrollJson(UnitVector()));

        var result = await service.EnrollAsync([0xFF, 0xD8, 0xFF]);

        Assert.Equal(UnitVector(), result.Vector);
        Assert.Equal("http://vision:8000/internal/face/enroll", handler.Request!.RequestUri!.ToString());
        Assert.Equal("test-key", handler.Request.Headers.GetValues("X-Service-Key").Single());
        Assert.Contains("\"image\":\"/9j/\"", handler.RequestBody);
    }

    [Theory]
    [InlineData(FaceIssue.NoFace)]
    [InlineData(FaceIssue.MultipleFaces)]
    [InlineData(FaceIssue.FaceTooSmall)]
    [InlineData(FaceIssue.ImageTooBlurry)]
    [InlineData(FaceIssue.InvalidImage)]
    public async Task Enroll_returns_the_photo_problem_to_the_candidate(string code)
    {
        var (service, _) = Create(HttpStatusCode.UnprocessableEntity, Error(code));

        var result = await service.EnrollAsync([1]);

        Assert.True(result.Rejected);
        Assert.Equal(code, result.Issue);
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity, """{"error":{"code":"VALIDATION_ERROR","message":"image: x"}}""")] // bizim isteğimiz bozuk
    [InlineData(HttpStatusCode.UnprocessableEntity, """{"detail":[{"msg":"x"}]}""")]                              // sözleşme dışı gövde
    [InlineData(HttpStatusCode.UnprocessableEntity, """{"error":{"code":"YENI_KOD","message":"x"}}""")]           // tanınmayan kod
    [InlineData(HttpStatusCode.Unauthorized, """{"error":{"code":"UNAUTHORIZED","message":"x"}}""")]              // anahtar uyuşmuyor
    [InlineData(HttpStatusCode.ServiceUnavailable, """{"error":{"code":"MODELS_NOT_READY","message":"x"}}""")]    // modeller yükleniyor
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.OK, "metin")]
    public async Task Enroll_treats_service_and_contract_problems_as_unavailable_not_as_a_bad_photo(HttpStatusCode status, string body)
    {
        var (service, _) = Create(status, body);

        await Assert.ThrowsAsync<FaceServiceUnavailableException>(() => service.EnrollAsync([1]));
    }

    [Fact]
    public async Task Enroll_rejects_a_vector_that_is_not_512_finite_and_unit_length()
    {
        var shortVector = new float[128];
        shortVector[0] = 1;
        var unnormalized = Enumerable.Repeat(0.1f, FaceVector.Dimension).ToArray();

        foreach (var vector in new[] { shortVector, unnormalized })
        {
            var (service, _) = Create(HttpStatusCode.OK, EnrollJson(vector));
            await Assert.ThrowsAsync<FaceServiceUnavailableException>(() => service.EnrollAsync([1]));
        }
    }

    [Fact]
    public async Task Unreachable_service_is_unavailable()
    {
        var service = Create(new FailingHandler());

        await Assert.ThrowsAsync<FaceServiceUnavailableException>(() => service.EnrollAsync([1]));
        await Assert.ThrowsAsync<FaceServiceUnavailableException>(() => Verify(service));
    }

    // ---------- doğrulama ----------

    [Fact]
    public async Task Verify_sends_frames_and_reference_and_returns_a_pass()
    {
        var (service, handler) = Create(HttpStatusCode.OK,
            """{"livenessScore":0.9,"similarity":0.7,"passed":true,"reason":null,"framesUsed":3}""");

        var result = await Verify(service);

        Assert.Equal(new FaceVerifyResult(0.9, 0.7, true), result);
        Assert.Equal("http://vision:8000/internal/face/verify", handler.Request!.RequestUri!.ToString());
        Assert.Contains("\"frames\":[\"AQ==\",\"Ag==\",\"Aw==\"]", handler.RequestBody);
        Assert.Contains("\"referenceVector\":[1,0,", handler.RequestBody);
    }

    [Theory]
    [InlineData(FaceIssue.LivenessFailed)]
    [InlineData(FaceIssue.IdentityMismatch)]
    [InlineData(FaceIssue.NoFace)]
    [InlineData(FaceIssue.MultipleFaces)]
    [InlineData("YENI_NEDEN")] // tanınmayan neden de ret: aday yine engelleniyor
    [InlineData(null)]
    public async Task Verify_failure_is_returned_with_its_reason(string? reason)
    {
        var body = JsonSerializer.Serialize(new { livenessScore = 0.2, similarity = 0.6, passed = false, reason, framesUsed = 3 });
        var (service, _) = Create(HttpStatusCode.OK, body);

        var result = await Verify(service);

        Assert.Equal(new FaceVerifyResult(0.2, 0.6, false, reason), result);
    }

    [Fact]
    public async Task Verify_counts_an_undecodable_frame_as_a_failed_attempt()
    {
        var (service, _) = Create(HttpStatusCode.UnprocessableEntity, Error(FaceIssue.InvalidImage));

        var result = await Verify(service);

        Assert.Equal(new FaceVerifyResult(0, 0, false, FaceIssue.InvalidImage), result);
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity, """{"error":{"code":"VALIDATION_ERROR","message":"frames: x"}}""")]
    [InlineData(HttpStatusCode.UnprocessableEntity, """{"error":{"code":"NO_FACE","message":"x"}}""")] // doğrulamada 200 ile gelmeli
    [InlineData(HttpStatusCode.ServiceUnavailable, """{"error":{"code":"MODELS_NOT_READY","message":"x"}}""")]
    [InlineData(HttpStatusCode.OK, """{"livenessScore":1.5,"similarity":0.7,"passed":true}""")]  // aralık dışı
    [InlineData(HttpStatusCode.OK, """{"livenessScore":0.9,"similarity":-1.2,"passed":true}""")] // aralık dışı
    [InlineData(HttpStatusCode.OK, """{"livenessScore":0.9,"passed":true}""")]                   // eksik alan
    [InlineData(HttpStatusCode.OK, """{"livenessScore":0.9,"similarity":0.7}""")]                // karar yok
    [InlineData(HttpStatusCode.OK, "metin")]
    public async Task Verify_treats_service_and_contract_problems_as_unavailable(HttpStatusCode status, string body)
    {
        var (service, _) = Create(status, body);

        await Assert.ThrowsAsync<FaceServiceUnavailableException>(() => Verify(service));
    }

    [Fact]
    public void Missing_service_key_stops_the_client()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() => VisionFaceService.Configure(new HttpClient(), configuration));
    }
}
