using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Sessions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AIGuard.IntegrationTests.Sessions;

/// <summary>
/// S-29: sorular yalnızca son <see cref="SessionMediaRateLimiter.QuestionFrameWindow"/> içinde kamera karesi kabul
/// edilen oturuma veriliyor. TestApi'nin "kare var" kabulü burada kaldırılıp gerçek hız sınırı kullanılıyor.
/// </summary>
public class SessionQuestionGateTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);

    private TestApi Api() => new(fx, services: s =>
    {
        s.AddSingleton<IMediaAnalyzer>(new RecordingMediaAnalyzer());
        s.AddSingleton<TimeProvider>(_clock);
        s.AddSingleton<ICameraPresence>(sp => sp.GetRequiredService<SessionMediaRateLimiter>());
    });

    private async Task<ExamSession> SessionWithQuestionAsync()
    {
        await using var db = fx.CreateContext();
        var exam = TestData.Exam();
        exam.Questions.Add(new Question { Order = 1, Text = "S1", Options = ["a", "b"], CorrectOptionIndex = 0 });
        var session = new ExamSession
        {
            Exam = exam,
            Candidate = TestData.Candidate(),
            State = SessionState.InProgress,
            StartedAt = _clock.GetUtcNow().AddMinutes(-1),
            EndsAt = _clock.GetUtcNow().AddMinutes(30),
        };
        db.ExamSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private static MultipartFormDataContent Frame()
    {
        var file = new ByteArrayContent(SessionMediaServiceTests.Jpeg);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return new MultipartFormDataContent { { file, "frame", "kare.jpg" } };
    }

    private static async Task<int> QuestionCountAsync(HttpClient client, Guid sessionId) =>
        (await client.GetFromJsonAsync<SessionView>($"/api/sessions/{sessionId}", Json))!.Questions.Count;

    [SkippableFact]
    public async Task Questions_are_hidden_until_a_camera_frame_arrives()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionWithQuestionAsync();
        var client = Api().As(session.Candidate);

        var before = await QuestionCountAsync(client, session.Id);
        await client.PostAsync($"/api/sessions/{session.Id}/frames", Frame());
        var after = await QuestionCountAsync(client, session.Id);

        Assert.Equal(0, before); // kamera açılmadan soru yok
        Assert.Equal(1, after);
    }

    [SkippableFact]
    public async Task Questions_are_hidden_again_when_frames_stop()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var session = await SessionWithQuestionAsync();
        var client = Api().As(session.Candidate);
        await client.PostAsync($"/api/sessions/{session.Id}/frames", Frame());

        _clock.Advance(SessionMediaRateLimiter.QuestionFrameWindow);
        var atEdge = await QuestionCountAsync(client, session.Id);
        _clock.Advance(TimeSpan.FromSeconds(1));
        var afterWindow = await QuestionCountAsync(client, session.Id);

        Assert.Equal(1, atEdge);
        Assert.Equal(0, afterWindow);
    }

    [SkippableFact]
    public async Task Another_sessions_frame_does_not_unlock_questions()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var mine = await SessionWithQuestionAsync();
        var other = await SessionWithQuestionAsync();
        var api = Api();

        await api.As(other.Candidate).PostAsync($"/api/sessions/{other.Id}/frames", Frame());

        Assert.Equal(0, await QuestionCountAsync(api.As(mine.Candidate), mine.Id));
    }
}
