using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIGuard.Api.Controllers;
using AIGuard.Application.Exams;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;

namespace AIGuard.IntegrationTests.Exams;

/// <summary>E3 uçları HTTP üzerinden (kabul kriteri: Swagger'dan sınav, sorular ve atamalar oluşturulabilir).</summary>
public class ExamApiTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    // API enum'ları metin olarak döndürüyor (K-05).
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private TestApi? _api;
    private TestApi Api => _api ??= new TestApi(fx);

    private async Task<User> UserAsync(UserRole role)
    {
        var user = TestData.UserWithRole(role);
        await using var db = fx.CreateContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static object ExamBody(string name = "Genel Yetenek Deneme", int duration = 60) => new
    {
        name,
        type = "GeneralAptitude",
        durationMinutes = duration,
        windowStartAt = DateTimeOffset.UtcNow.AddHours(-1),
        windowEndAt = DateTimeOffset.UtcNow.AddDays(2),
        retentionDays = 30,
    };

    private static async Task<Guid> CreatedIdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;
    }

    private async Task<Guid> ExamAsync(HttpClient admin) => await CreatedIdAsync(await admin.PostAsJsonAsync("/api/exams", ExamBody()));

    private async Task StartSessionAsync(Guid examId, Guid candidateId)
    {
        await using var db = fx.CreateContext();
        db.ExamSessions.Add(new ExamSession { ExamId = examId, CandidateId = candidateId });
        await db.SaveChangesAsync();
    }

    [SkippableFact]
    public async Task Admin_creates_exam_questions_and_assignments()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var admin = Api.As(await UserAsync(UserRole.Admin));
        var candidate = await UserAsync(UserRole.Candidate);
        var reviewer = await UserAsync(UserRole.Reviewer);

        var examId = await ExamAsync(admin);
        await CreatedIdAsync(await admin.PostAsJsonAsync($"/api/exams/{examId}/questions",
            new { text = "2 + 2 = ?", options = new[] { "3", "4" }, correctOptionIndex = 1 }));
        var secondId = await CreatedIdAsync(await admin.PostAsJsonAsync($"/api/exams/{examId}/questions",
            new { text = "She ___ to school.", options = new[] { "go", "goes", "gone" }, correctOptionIndex = 1 }));
        // Sıralama: ikinci soruyu başa al.
        var reorder = await admin.PutAsJsonAsync($"/api/questions/{secondId}",
            new { text = "She ___ to school.", options = new[] { "go", "goes", "gone" }, correctOptionIndex = 1, order = 0 });
        await CreatedIdAsync(await admin.PostAsJsonAsync($"/api/exams/{examId}/assignments", new { userId = candidate.Id, role = "Candidate" }));
        await CreatedIdAsync(await admin.PostAsJsonAsync($"/api/exams/{examId}/assignments", new { userId = reviewer.Id, role = "Reviewer" }));

        var questions = await admin.GetFromJsonAsync<List<QuestionDetail>>($"/api/exams/{examId}/questions", Json);
        var assignments = await admin.GetFromJsonAsync<List<AssignmentDetail>>($"/api/exams/{examId}/assignments", Json);
        var exam = await admin.GetFromJsonAsync<ExamSummary>($"/api/exams/{examId}", Json);

        Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);
        Assert.Equal(new[] { secondId }, questions!.Take(1).Select(q => q.Id)); // sıraya göre
        Assert.Equal(1, questions![1].CorrectOptionIndex); // yönetici doğru seçeneği görüyor
        Assert.Equal(2, assignments!.Count);
        Assert.Equal(2, exam!.QuestionCount);
        Assert.Equal(exam.WindowEndAt.AddMinutes(-60), exam.LatestStartAt);
        Assert.Null(exam.AttemptUsed); // yalnızca adaya dönüyor
    }

    [SkippableFact]
    public async Task Candidate_and_reviewer_see_only_assigned_exams_without_correct_answers()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var admin = Api.As(await UserAsync(UserRole.Admin));
        var candidate = await UserAsync(UserRole.Candidate);
        var reviewer = await UserAsync(UserRole.Reviewer);
        var assigned = await ExamAsync(admin);
        var other = await ExamAsync(admin);
        await admin.PostAsJsonAsync($"/api/exams/{assigned}/questions", new { text = "Soru", options = new[] { "a", "b" }, correctOptionIndex = 0 });
        await admin.PostAsJsonAsync($"/api/exams/{assigned}/assignments", new { userId = candidate.Id, role = "Candidate" });
        await admin.PostAsJsonAsync($"/api/exams/{assigned}/assignments", new { userId = reviewer.Id, role = "Reviewer" });

        var candidateList = await Api.As(candidate).GetAsync("/api/exams");
        var candidateJson = await candidateList.Content.ReadAsStringAsync();
        var candidateExams = JsonSerializer.Deserialize<List<ExamSummary>>(candidateJson, Json)!;
        var reviewerExams = await Api.As(reviewer).GetFromJsonAsync<List<ExamSummary>>("/api/exams", Json);
        var otherAsCandidate = await Api.As(candidate).GetAsync($"/api/exams/{other}");
        var questionsAsCandidate = await Api.As(candidate).GetAsync($"/api/exams/{assigned}/questions");

        Assert.Equal(new[] { assigned }, candidateExams.Select(e => e.Id));
        Assert.Equal(new[] { assigned }, reviewerExams!.Select(e => e.Id));
        Assert.False(candidateExams[0].AttemptUsed);
        Assert.DoesNotContain("correct", candidateJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NotFound, otherAsCandidate.StatusCode); // atanmadığı sınav yok sayılıyor
        Assert.Equal(HttpStatusCode.Forbidden, questionsAsCandidate.StatusCode); // doğru seçenekli liste yalnızca yöneticiye
    }

    [SkippableFact]
    public async Task Only_admin_can_create_or_change_exams()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var reviewer = Api.As(await UserAsync(UserRole.Reviewer));
        var candidate = Api.As(await UserAsync(UserRole.Candidate));

        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.PostAsJsonAsync("/api/exams", ExamBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await candidate.PostAsJsonAsync("/api/exams", ExamBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Api.Anonymous().GetAsync("/api/exams")).StatusCode);
    }

    [SkippableFact]
    public async Task Invalid_exam_and_question_are_rejected_with_a_message()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var admin = Api.As(await UserAsync(UserRole.Admin));
        var examId = await ExamAsync(admin);

        // Pencere 2 gün, sınav 3 gün: son başlangıç zamanı pencere başlangıcından önce kalır.
        var tooLong = await admin.PostAsJsonAsync("/api/exams", ExamBody(duration: 600));
        var badWindow = await admin.PostAsJsonAsync("/api/exams", new
        {
            name = "X", type = "GeneralAptitude", durationMinutes = 60,
            windowStartAt = DateTimeOffset.UtcNow, windowEndAt = DateTimeOffset.UtcNow.AddMinutes(30),
        });
        var oneOption = await admin.PostAsJsonAsync($"/api/exams/{examId}/questions", new { text = "S", options = new[] { "a" }, correctOptionIndex = 0 });

        Assert.Equal(HttpStatusCode.Created, tooLong.StatusCode); // 600 dk, 2 günlük pencereye sığıyor
        Assert.Equal(HttpStatusCode.BadRequest, badWindow.StatusCode);
        Assert.Contains("sınav süresinden kısa", await badWindow.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, oneOption.StatusCode);
    }

    [SkippableFact]
    public async Task Exam_content_is_locked_once_a_session_exists()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var admin = Api.As(await UserAsync(UserRole.Admin));
        var candidate = await UserAsync(UserRole.Candidate);
        var examId = await ExamAsync(admin);
        var questionId = await CreatedIdAsync(await admin.PostAsJsonAsync($"/api/exams/{examId}/questions",
            new { text = "S", options = new[] { "a", "b" }, correctOptionIndex = 0 }));
        var assignmentId = await CreatedIdAsync(await admin.PostAsJsonAsync($"/api/exams/{examId}/assignments",
            new { userId = candidate.Id, role = "Candidate" }));
        await StartSessionAsync(examId, candidate.Id);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/exams/{examId}", ExamBody("Yeni ad"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/exams/{examId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/questions/{questionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/exams/{examId}/questions",
            new { text = "S2", options = new[] { "a", "b" }, correctOptionIndex = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/assignments/{assignmentId}")).StatusCode);
    }

    [SkippableFact]
    public async Task Exam_without_sessions_can_be_updated_and_deleted()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var admin = Api.As(await UserAsync(UserRole.Admin));
        var examId = await ExamAsync(admin);

        var update = await admin.PutAsJsonAsync($"/api/exams/{examId}", ExamBody("Güncel ad", duration: 45));
        var updated = await admin.GetFromJsonAsync<ExamSummary>($"/api/exams/{examId}", Json);
        var delete = await admin.DeleteAsync($"/api/exams/{examId}");
        var afterDelete = await admin.GetAsync($"/api/exams/{examId}");

        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        Assert.Equal(("Güncel ad", 45), (updated!.Name, updated.DurationMinutes));
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [SkippableFact]
    public async Task Assignment_rules_duplicate_and_role_mismatch()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var admin = Api.As(await UserAsync(UserRole.Admin));
        var candidate = await UserAsync(UserRole.Candidate);
        var examId = await ExamAsync(admin);

        await admin.PostAsJsonAsync($"/api/exams/{examId}/assignments", new { userId = candidate.Id, role = "Candidate" });
        var duplicate = await admin.PostAsJsonAsync($"/api/exams/{examId}/assignments", new { userId = candidate.Id, role = "Candidate" });
        // Aday hesabı değerlendirici olarak atanamaz.
        var mismatch = await admin.PostAsJsonAsync($"/api/exams/{examId}/assignments", new { userId = candidate.Id, role = "Reviewer" });
        var unknownUser = await admin.PostAsJsonAsync($"/api/exams/{examId}/assignments", new { userId = Guid.NewGuid(), role = "Candidate" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownUser.StatusCode);
    }

    [SkippableFact]
    public async Task Candidate_sees_that_the_single_attempt_is_used()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var admin = Api.As(await UserAsync(UserRole.Admin));
        var candidate = await UserAsync(UserRole.Candidate);
        var examId = await ExamAsync(admin);
        await admin.PostAsJsonAsync($"/api/exams/{examId}/assignments", new { userId = candidate.Id, role = "Candidate" });
        await using (var db = fx.CreateContext())
        {
            db.ExamAssignments.Single(a => a.ExamId == examId && a.UserId == candidate.Id).AttemptUsedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        var exam = await Api.As(candidate).GetFromJsonAsync<ExamSummary>($"/api/exams/{examId}", Json);

        Assert.True(exam!.AttemptUsed);
    }

    [SkippableFact]
    public async Task Swagger_document_is_available_in_development()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

        var response = await Api.Anonymous().GetAsync("/swagger/v1/swagger.json");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/exams/{id}/questions", json);
        Assert.Contains("/api/assignments/{id}", json);
    }
}
