using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIGuard.Api.Controllers;
using AIGuard.Application.Sessions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AIGuard.IntegrationTests.Sessions;

/// <summary>E4 uçları HTTP üzerinden, gerçek saatle.</summary>
public class SessionsApiTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private TestApi? _api;
    private TestApi Api => _api ??= new TestApi(fx);

    private async Task<(Exam Exam, User Candidate)> ExamAsync()
    {
        await using var db = fx.CreateContext();
        var candidate = TestData.Candidate();
        var exam = TestData.Exam(); // pencere şu an açık
        exam.Questions.Add(new Question { Order = 1, Text = "S1", Options = ["a", "b"], CorrectOptionIndex = 1 });
        exam.Assignments.Add(new ExamAssignment { User = candidate, Role = AssignmentRole.Candidate });
        db.Exams.Add(exam);
        await db.SaveChangesAsync();
        return (exam, candidate);
    }

    /// <summary>Rıza, eşleştirme ve doğrulama (A3, Y1) atlanıp sınav başlatılıyor.</summary>
    private async Task BeginAsync(Guid sessionId)
    {
        await using (var db = fx.CreateContext())
        {
            (await db.ExamSessions.SingleAsync(s => s.Id == sessionId)).State = SessionState.IdentityVerification;
            await db.SaveChangesAsync();
        }

        using var scope = Api.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SessionService>().BeginExamAsync(sessionId);
    }

    [SkippableFact]
    public async Task Candidate_starts_answers_and_submits()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (exam, candidate) = await ExamAsync();
        var client = Api.As(candidate);

        var start = await client.PostAsync($"/api/exams/{exam.Id}/sessions", null);
        var started = await start.Content.ReadFromJsonAsync<StartSessionResponse>(Json);
        var restart = await client.PostAsync($"/api/exams/{exam.Id}/sessions", null);
        var beforeBegin = await client.GetFromJsonAsync<SessionView>($"/api/sessions/{started!.SessionId}", Json);
        await BeginAsync(started.SessionId);
        var view = await client.GetAsync($"/api/sessions/{started.SessionId}");
        var viewJson = await view.Content.ReadAsStringAsync();
        var running = JsonSerializer.Deserialize<SessionView>(viewJson, Json)!;
        var questionId = running.Questions.Single().Id;
        var answer = await client.PostAsJsonAsync($"/api/sessions/{started.SessionId}/answers",
            new { questionId, selectedOption = 1, seq = 1, clientTime = DateTimeOffset.UtcNow });
        var retry = await client.PostAsJsonAsync($"/api/sessions/{started.SessionId}/answers",
            new { questionId, selectedOption = 1, seq = 1 });
        var submit = await client.PostAsync($"/api/sessions/{started.SessionId}/submit", null);
        var again = await client.PostAsync($"/api/sessions/{started.SessionId}/submit", null);
        var after = await client.PostAsJsonAsync($"/api/sessions/{started.SessionId}/answers", new { questionId, selectedOption = 0, seq = 2 });
        var restartAfterSubmit = await client.PostAsync($"/api/exams/{exam.Id}/sessions", null);

        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        Assert.Equal(SessionState.ConsentPending, started.State);
        Assert.Equal(HttpStatusCode.OK, restart.StatusCode); // aynı oturuma dönüş
        Assert.Empty(beforeBegin!.Questions); // sınav başlamadan soru yok
        Assert.Equal(SessionState.InProgress, running.State);
        Assert.NotNull(running.EndsAt);
        Assert.DoesNotContain("correct", viewJson, StringComparison.OrdinalIgnoreCase); // doğru seçenek adaya gitmiyor
        Assert.Equal(HttpStatusCode.Created, answer.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode); // kuyruk tekrar gönderdi
        Assert.Equal(HttpStatusCode.NoContent, submit.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, after.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, restartAfterSubmit.StatusCode); // tek oturum hakkı
        Assert.Contains("oturum hakkınızı kullandınız", await restartAfterSubmit.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task Undefined_transition_returns_409()
    {
        // Kabul kriteri: tanımsız bir durum geçişi denendiğinde API reddeder.
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (exam, candidate) = await ExamAsync();
        var client = Api.As(candidate);
        var started = await (await client.PostAsync($"/api/exams/{exam.Id}/sessions", null)).Content.ReadFromJsonAsync<StartSessionResponse>(Json);

        var submit = await client.PostAsync($"/api/sessions/{started!.SessionId}/submit", null); // ConsentPending → Completed yok

        Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode);
    }

    [SkippableFact]
    public async Task Other_candidates_and_anonymous_cannot_use_the_session()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var (exam, candidate) = await ExamAsync();
        var (_, other) = await ExamAsync();
        var started = await (await Api.As(candidate).PostAsync($"/api/exams/{exam.Id}/sessions", null)).Content.ReadFromJsonAsync<StartSessionResponse>(Json);

        var asOther = await Api.As(other).GetAsync($"/api/sessions/{started!.SessionId}");
        var otherStart = await Api.As(other).PostAsync($"/api/exams/{exam.Id}/sessions", null); // atanmamış
        var anonymous = await Api.Anonymous().GetAsync($"/api/sessions/{started.SessionId}");

        Assert.Equal(HttpStatusCode.NotFound, asOther.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherStart.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}
