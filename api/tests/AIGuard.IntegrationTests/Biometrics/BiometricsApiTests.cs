using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using AIGuard.Application.Abstractions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AIGuard.IntegrationTests.Biometrics.BiometricTestHelpers;

namespace AIGuard.IntegrationTests.Biometrics;

/// <summary>Biyometrik uçların HTTP davranışı; görüntü servisi sahte.</summary>
public class BiometricsApiTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private WebApplicationFactory<Program> Factory(FakeFaceService? faces = null) =>
        new TestApi(fx).Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting(AesGcmFieldEncryptor.KeySetting, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            b.ConfigureTestServices(s =>
            {
                s.AddSingleton<IFaceService>(faces ?? new FakeFaceService());
                s.AddSingleton<IEvidenceStore, MemoryEvidenceStore>();
            });
        });

    private static HttpClient As(WebApplicationFactory<Program> factory, User user)
    {
        var client = factory.CreateClient();
        var token = factory.Services.GetRequiredService<ITokenService>().CreateAccessToken(user).Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static MultipartFormDataContent PhotoForm(byte[] photo, bool consent = true)
    {
        var file = new ByteArrayContent(photo);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return new MultipartFormDataContent
        {
            { file, "photo", "foto.jpg" },
            { new StringContent(consent ? "true" : "false"), "consent" },
            { new StringContent(ConsentText.InitialBiometricVersion), "consentTextVersion" },
        };
    }

    private static MultipartFormDataContent FramesForm()
    {
        var form = new MultipartFormDataContent();
        foreach (var (frame, i) in Frames().Select((f, i) => (f, i)))
        {
            form.Add(new ByteArrayContent(frame), "frames", $"kare{i}.jpg");
        }

        return form;
    }

    private async Task<(Guid SessionId, User Candidate)> PendingAsync()
    {
        await using var db = fx.CreateContext();
        var session = await PendingSessionAsync(db, SessionState.ConsentPending, enrolled: false);
        return (session.Id, await db.Users.AsNoTracking().SingleAsync(u => u.Id == session.CandidateId));
    }

    [SkippableFact]
    public async Task Candidate_uploads_photo_gives_session_consent_and_passes_verification()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (sessionId, candidate) = await PendingAsync();
        var client = As(Factory(), candidate);

        var upload = await client.PostAsync("/api/me/photo", PhotoForm(Jpeg()));
        var consent = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/consent",
            new { accepted = true, textVersion = ConsentText.InitialSessionVersion });

        // Ajan adımı Y1'in uçlarıyla; burada doğrudan doğrulama adımına geçiriliyor.
        await using (var db = fx.CreateContext())
        {
            await db.ExamSessions.Where(s => s.Id == sessionId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.State, SessionState.IdentityVerification));
        }

        var verify = await client.PostAsync($"/api/sessions/{sessionId}/verify", FramesForm());

        Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);
        Assert.Equal(HttpStatusCode.OK, consent.StatusCode);
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        Assert.Contains("InProgress", await verify.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task Requests_without_consent_or_with_bad_input_are_rejected()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (sessionId, candidate) = await PendingAsync();
        var client = As(Factory(), candidate);

        var noConsent = await client.PostAsync("/api/me/photo", PhotoForm(Jpeg(), consent: false));
        var notImage = await client.PostAsync("/api/me/photo", PhotoForm("<svg></svg>"u8.ToArray()));
        var missingAnswer = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/consent", new { textVersion = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, noConsent.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, notImage.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingAnswer.StatusCode); // eksik alan oturumu iptal etmiyor
    }

    [SkippableFact]
    public async Task Rejected_photo_tells_the_candidate_what_to_fix()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (_, candidate) = await PendingAsync();
        var faces = new FakeFaceService { EnrollResult = FaceEnrollResult.Rejection(FaceIssue.MultipleFaces) };

        var response = await As(Factory(faces), candidate).PostAsync("/api/me/photo", PhotoForm(Jpeg()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("yalnızca siz olmalısınız", await response.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task Vision_outage_returns_503_and_is_not_counted_against_the_candidate()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (sessionId, candidate) = await PendingAsync();
        var faces = new FakeFaceService();
        var client = As(Factory(faces), candidate);
        await client.PostAsync("/api/me/photo", PhotoForm(Jpeg()));

        faces.Unavailable = true;
        var upload = await client.PostAsync("/api/me/photo", PhotoForm(Jpeg())); // yeniden yükleme, rıza adımında
        await using (var db = fx.CreateContext())
        {
            await db.ExamSessions.Where(s => s.Id == sessionId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.State, SessionState.IdentityVerification));
        }

        var verify = await client.PostAsync($"/api/sessions/{sessionId}/verify", FramesForm());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, upload.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, verify.StatusCode);
        await using (var db = fx.CreateContext())
        {
            var session = await db.ExamSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
            Assert.Equal(0, session.VerificationAttempts);
            Assert.Equal(SessionState.IdentityVerification, session.State);
        }
    }

    [SkippableFact]
    public async Task Only_the_candidate_who_owns_the_session_can_use_the_endpoints()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var factory = Factory();
        var (sessionId, _) = await PendingAsync();
        var (_, otherCandidate) = await PendingAsync();
        var reviewer = TestData.Candidate();
        reviewer.Role = UserRole.Reviewer;
        await using (var db = fx.CreateContext())
        {
            db.Users.Add(reviewer);
            await db.SaveChangesAsync();
        }

        var anonymous = await factory.CreateClient().PostAsync("/api/me/photo", PhotoForm(Jpeg()));
        var asReviewer = await As(factory, reviewer).PostAsync("/api/me/photo", PhotoForm(Jpeg()));
        var foreignSession = await As(factory, otherCandidate).PostAsJsonAsync($"/api/sessions/{sessionId}/consent",
            new { accepted = false });

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asReviewer.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignSession.StatusCode); // başkasının oturumunu iptal edemiyor
    }
}
