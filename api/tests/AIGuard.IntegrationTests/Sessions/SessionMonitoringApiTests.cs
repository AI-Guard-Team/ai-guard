using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.Sessions;

/// <summary>
/// S4 uçlarının HTTP davranışı: <c>frames</c>, <c>audio</c> ve <c>permission</c>. Yetki, oturum sahipliği,
/// çok parçalı gövde ve yanıtta analiz bilgisi olmaması.
/// </summary>
public class SessionMonitoringApiTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private readonly RecordingMediaAnalyzer _analyzer = new();
    private TestApi? _api;
    private TestApi Api => _api ??= new TestApi(fx, services: s => s.AddSingleton<IMediaAnalyzer>(_analyzer));

    private async Task<ExamSession> SessionAsync(SessionState state = SessionState.InProgress)
    {
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db, state);
        // Testler gerçek saatle çalışıyor; sınav şu an sürüyor olmalı.
        session.StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        session.EndsAt = DateTimeOffset.UtcNow.AddMinutes(30);
        await db.SaveChangesAsync();
        return session;
    }

    private static MultipartFormDataContent Form(string field, byte[] data, string fileName, string contentType)
    {
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, field, fileName } };
    }

    private static MultipartFormDataContent FrameForm(byte[]? data = null) =>
        Form("frame", data ?? SessionMediaServiceTests.Jpeg, "kare.jpg", "image/jpeg");

    private static MultipartFormDataContent AudioForm(byte[]? data = null) =>
        Form("audio", data ?? SessionMediaServiceTests.WebM, "ses.webm", "audio/webm");

    private async Task<ExamSession> ReloadAsync(Guid id)
    {
        await using var db = fx.CreateContext();
        return await db.ExamSessions.AsNoTracking().SingleAsync(s => s.Id == id);
    }

    [SkippableFact]
    public async Task Frame_and_audio_are_accepted_with_an_empty_202()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionAsync();
        var client = Api.As(session.Candidate);

        var frame = await client.PostAsync($"/api/sessions/{session.Id}/frames", FrameForm());
        var audio = await client.PostAsync($"/api/sessions/{session.Id}/audio", AudioForm());

        Assert.Equal(HttpStatusCode.Accepted, frame.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, audio.StatusCode);
        // Aday analiz sonucunu görmemeli.
        Assert.Equal(string.Empty, await frame.Content.ReadAsStringAsync());
        Assert.Single(_analyzer.Frames);
        Assert.Single(_analyzer.Audio);
    }

    [SkippableFact]
    public async Task Content_type_header_is_not_trusted()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionAsync();
        var client = Api.As(session.Candidate);

        var response = await client.PostAsync($"/api/sessions/{session.Id}/frames",
            Form("frame", "<svg></svg>"u8.ToArray(), "kare.jpg", "image/jpeg"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_analyzer.Frames);
    }

    [SkippableFact]
    public async Task Missing_or_misnamed_field_is_400()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionAsync();
        var client = Api.As(session.Candidate);

        var wrongField = await client.PostAsync($"/api/sessions/{session.Id}/frames",
            Form("image", SessionMediaServiceTests.Jpeg, "kare.jpg", "image/jpeg"));
        var oversized = await client.PostAsync($"/api/sessions/{session.Id}/audio",
            AudioForm(new byte[Application.Sessions.SessionMediaService.MaxAudioBytes + 1]));

        Assert.Equal(HttpStatusCode.BadRequest, wrongField.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
    }

    [SkippableFact]
    public async Task Back_to_back_frame_is_429()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionAsync();
        var client = Api.As(session.Candidate);

        await client.PostAsync($"/api/sessions/{session.Id}/frames", FrameForm());
        var second = await client.PostAsync($"/api/sessions/{session.Id}/frames", FrameForm());

        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [SkippableFact]
    public async Task Frames_while_waiting_for_permission_are_409()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionAsync(SessionState.PermissionPending);

        var response = await Api.As(session.Candidate).PostAsync($"/api/sessions/{session.Id}/frames", FrameForm());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [SkippableFact]
    public async Task Permission_round_trip_hides_questions_and_records_the_gap()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionAsync();
        var client = Api.As(session.Candidate);
        var url = $"/api/sessions/{session.Id}/permission";

        var revoked = await client.PostAsJsonAsync(url, new { granted = false });
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Contains("PermissionPending", await revoked.Content.ReadAsStringAsync());
        Assert.Equal(SessionState.PermissionPending, (await ReloadAsync(session.Id)).State);

        // Tekrar bildirmek hata değil.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(url, new { granted = false })).StatusCode);

        var restored = await client.PostAsJsonAsync(url, new { granted = true });
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Contains("InProgress", await restored.Content.ReadAsStringAsync());

        await using var db = fx.CreateContext();
        var gap = await db.Events.AsNoTracking().SingleAsync(e => e.ExamSessionId == session.Id);
        Assert.Equal(EventType.PERMISSION_REVOKED, gap.Type);
        Assert.True((await ReloadAsync(session.Id)).Flags.HasFlag(SessionFlags.PermissionGap));
    }

    [SkippableFact]
    public async Task Client_supplied_duration_is_ignored()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionAsync();
        var client = Api.As(session.Candidate);
        var url = $"/api/sessions/{session.Id}/permission";

        await client.PostAsJsonAsync(url, new { granted = false });
        await client.PostAsJsonAsync(url, new { granted = true, durationSec = 0.0 });

        await using var db = fx.CreateContext();
        var gap = await db.Events.AsNoTracking().SingleAsync(e => e.ExamSessionId == session.Id);
        // Süre sunucunun iki istek arasında ölçtüğü değer; istemcinin gönderdiği 0 değil, 1 saati de geçmiyor.
        Assert.InRange(gap.DurationSec, 0, 60);
        Assert.True((await ReloadAsync(session.Id)).Flags.HasFlag(SessionFlags.PermissionGap));
    }

    [SkippableFact]
    public async Task Permission_without_granted_is_400_and_changes_nothing()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionAsync();

        var response = await Api.As(session.Candidate).PostAsJsonAsync($"/api/sessions/{session.Id}/permission", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(SessionState.InProgress, (await ReloadAsync(session.Id)).State);
    }

    [SkippableTheory]
    [InlineData("frames")]
    [InlineData("audio")]
    [InlineData("permission")]
    public async Task Another_candidates_session_is_404(string endpoint)
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var victim = await SessionAsync();
        var other = await SessionAsync();

        var response = await Send(Api.As(other.Candidate), victim.Id, endpoint);

        // 403 değil 404: başka oturumun var olduğu bilgisi verilmiyor (K-20).
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(SessionState.InProgress, (await ReloadAsync(victim.Id)).State);
        Assert.Empty(_analyzer.Frames);
    }

    [SkippableTheory]
    [InlineData("frames")]
    [InlineData("audio")]
    [InlineData("permission")]
    public async Task Anonymous_is_401_and_other_roles_are_403(string endpoint)
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionAsync();
        var reviewer = TestData.UserWithRole(UserRole.Reviewer);
        await using (var db = fx.CreateContext())
        {
            db.Users.Add(reviewer);
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(Api.Anonymous(), session.Id, endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(Api.As(reviewer), session.Id, endpoint)).StatusCode);
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, Guid sessionId, string endpoint) => endpoint switch
    {
        "frames" => client.PostAsync($"/api/sessions/{sessionId}/frames", FrameForm()),
        "audio" => client.PostAsync($"/api/sessions/{sessionId}/audio", AudioForm()),
        _ => client.PostAsJsonAsync($"/api/sessions/{sessionId}/permission", new { granted = false }),
    };
}
