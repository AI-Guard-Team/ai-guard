using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Review;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.IntegrationTests.Biometrics;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AIGuard.IntegrationTests.Review.ReviewTestHelpers;

namespace AIGuard.IntegrationTests.Review;

/// <summary>A4 uçlarının HTTP davranışı: yetki matrisi (K-31), imzalı kanıt bağlantısı ve SignalR bildirimi.</summary>
public class ReviewApiTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly MemoryEvidenceStore _evidence = new();

    private WebApplicationFactory<Program> Factory() =>
        new TestApi(fx).Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<IEvidenceStore>(_evidence)));

    private static string Token(WebApplicationFactory<Program> factory, User user) =>
        factory.Services.GetRequiredService<ITokenService>().CreateAccessToken(user).Token;

    private static HttpClient As(WebApplicationFactory<Program> factory, User user)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(factory, user));
        return client;
    }

    [SkippableFact]
    public async Task Endpoints_follow_the_access_matrix()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        ReviewScenario s;
        ExamSession session;
        Event phone;
        User candidate;
        await using (var db = fx.CreateContext())
        {
            s = await ScenarioAsync(db);
            session = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky, events: new EventSpec(EventType.PHONE, 10));
            phone = await db.Events.AsNoTracking().SingleAsync(e => e.ExamSessionId == session.Id);
            candidate = await db.Users.AsNoTracking().SingleAsync(u => u.Id == session.CandidateId);
        }

        var factory = Factory();
        var list = $"/api/review/exams/{s.Exam.Id}/sessions";
        var detail = $"/api/review/sessions/{session.Id}";
        var report = $"/api/reports/exams/{s.Exam.Id}/sessions/{session.Id}";
        var reviewer = As(factory, s.Reviewer);
        var other = As(factory, s.OtherReviewer);
        var admin = As(factory, s.Admin);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(list)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(factory, candidate).GetAsync(list)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(factory, candidate).GetAsync(report)).StatusCode);

        // Atanmamış değerlendirici: oturumun varlığı da söylenmiyor.
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(list)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(detail)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(report)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/events/{phone.Id}/false-alarm", null)).StatusCode);

        // Admin okur, yazamaz.
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(list)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(report)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await admin.PostAsJsonAsync($"{detail}/decision", new { decision = "Clean" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"{detail}/notes", new { text = "not" })).StatusCode);

        // Atanmış değerlendirici.
        var listed = await reviewer.GetFromJsonAsync<ReviewSessionList>($"{list}?risky=true&flags=Risky&sort=Score", Json);
        Assert.Equal([session.Id], listed!.Sessions.Select(r => r.SessionId));
        Assert.Equal(1, listed.PendingCount);
        Assert.Equal(HttpStatusCode.NoContent, (await reviewer.PostAsync($"/api/events/{phone.Id}/false-alarm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await reviewer.PostAsJsonAsync($"{detail}/notes", new { text = "Telefon değil, hesap makinesi." })).StatusCode);
        Assert.Equal("no-store", (await reviewer.GetAsync(list)).Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync($"{detail}/decision", new { note = "kararsız" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await reviewer.PostAsJsonAsync($"{detail}/decision", new { decision = "Clean" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await reviewer.PostAsJsonAsync($"{detail}/decision", new { decision = "Violation" })).StatusCode);

        var shown = await reviewer.GetAsync(detail);
        Assert.Equal("no-store", shown.Headers.CacheControl?.ToString());
        var body = await shown.Content.ReadFromJsonAsync<ReviewSessionDetail>(Json);
        Assert.Equal(ReviewStatus.Decided, body!.Status);
        Assert.Equal(ReviewDecisionType.Clean, body.Decision!.Decision);
    }

    [SkippableFact]
    public async Task Evidence_link_opens_the_frame_without_a_token_and_cannot_be_altered()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        ReviewScenario s;
        ExamSession session;
        var image = BiometricTestHelpers.Jpeg();
        await using (var db = fx.CreateContext())
        {
            s = await ScenarioAsync(db);
            session = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky,
                events: [new EventSpec(EventType.PHONE, 10, WithEvidence: true), new EventSpec(EventType.PHONE, 12, WithEvidence: true)]);

            // Depodaki gerçek dosya adı: kare şifreli depoya (burada bellekteki sahte depoya) yazılmış gibi.
            var frames = await db.EvidenceFrames.Where(f => f.Event.ExamSessionId == session.Id).ToListAsync();
            foreach (var frame in frames)
            {
                frame.StorageKey = (await _evidence.SaveAsync(image)).StorageKey;
            }

            await db.SaveChangesAsync();
        }

        var factory = Factory();
        var detail = await As(factory, s.Reviewer).GetFromJsonAsync<ReviewSessionDetail>($"/api/review/sessions/{session.Id}", Json);
        var urls = detail!.Events.Select(e => e.EvidenceUrl!).ToList();
        var anonymous = factory.CreateClient();

        var opened = await anonymous.GetAsync(urls[0]);
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.Equal("image/jpeg", opened.Content.Headers.ContentType?.MediaType);
        Assert.Equal(image, await opened.Content.ReadAsByteArrayAsync());

        // Başka karenin imzasıyla, değiştirilmiş son tarihle ya da imzasız açılmıyor.
        var otherFrameId = urls[1].Split('?')[0].Split('/')[^1];
        var query = urls[0].Split('?')[1];
        var exp = long.Parse(System.Web.HttpUtility.ParseQueryString(query)["exp"]!);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/evidence/{otherFrameId}?{query}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(urls[0].Replace($"exp={exp}", $"exp={exp + 60}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(urls[0].Split('?')[0])).StatusCode);
    }

    /// <summary>
    /// Gövde boyutu sınırı TestServer'da uygulanmıyor, yalnızca Kestrel'de (SessionEventsApiTests'teki not);
    /// bu yüzden sınırın yazma uçlarında tanımlı olduğu doğrudan kontrol ediliyor.
    /// </summary>
    [Theory]
    [InlineData(nameof(AIGuard.Api.Controllers.ReviewController.Decide))]
    [InlineData(nameof(AIGuard.Api.Controllers.ReviewController.AddNote))]
    public void Write_endpoints_limit_the_request_body(string action)
    {
        var limit = typeof(AIGuard.Api.Controllers.ReviewController).GetMethod(action)!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute), inherit: false);

        Assert.Single(limit);
    }

    [SkippableFact]
    public async Task Write_endpoints_reject_admin_candidate_and_malformed_decisions()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        ReviewScenario s;
        ExamSession session;
        Event phone;
        User candidate;
        await using (var db = fx.CreateContext())
        {
            s = await ScenarioAsync(db);
            session = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky, events: new EventSpec(EventType.PHONE, 10));
            phone = await db.Events.AsNoTracking().SingleAsync(e => e.ExamSessionId == session.Id);
            candidate = await db.Users.AsNoTracking().SingleAsync(u => u.Id == session.CandidateId);
        }

        var factory = Factory();
        var reviewer = As(factory, s.Reviewer);
        var decision = $"/api/review/sessions/{session.Id}/decision";

        Assert.Equal(HttpStatusCode.Forbidden, (await As(factory, s.Admin).PostAsync($"/api/events/{phone.Id}/false-alarm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(factory, candidate).PostAsync($"/api/events/{phone.Id}/false-alarm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(factory, candidate).PostAsJsonAsync(decision, new { decision = "Clean" })).StatusCode);

        // Karar adla geliyor; sayı ya da bilinmeyen ad kabul edilmiyor (K-05).
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync(decision, new { decision = "Guilty" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync(decision, new { decision = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reviewer.PostAsJsonAsync($"/api/review/sessions/{Guid.NewGuid()}/decision", new { decision = "Clean" })).StatusCode);

        await using var check = fx.CreateContext();
        Assert.Equal(SessionState.Completed, (await check.ExamSessions.SingleAsync(x => x.Id == session.Id)).State);
        Assert.False((await check.Events.SingleAsync(e => e.Id == phone.Id)).IsFalseAlarm);
    }

    [SkippableFact]
    public async Task Token_in_the_query_string_is_accepted_only_by_the_hub()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        ReviewScenario s;
        await using (var db = fx.CreateContext())
        {
            s = await ScenarioAsync(db);
        }

        var factory = Factory();
        var token = Uri.EscapeDataString(Token(factory, s.Reviewer));

        // REST uçlarında belirteç URL'de taşınmasın (günlüklere ve tarayıcı geçmişine düşer).
        var rest = await factory.CreateClient().GetAsync($"/api/review/exams/{s.Exam.Id}/sessions?access_token={token}");
        Assert.Equal(HttpStatusCode.Unauthorized, rest.StatusCode);

        var anonymous = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/review"), o =>
            {
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => anonymous.StartAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);

        var forged = Hub(factory, Token(factory, s.Reviewer) + "x"); // imzası bozuk belirteç
        Assert.Equal(HttpStatusCode.Unauthorized, (await Assert.ThrowsAsync<HttpRequestException>(() => forged.StartAsync())).StatusCode);
    }

    [SkippableFact]
    public async Task Evidence_deleted_by_retention_is_not_found()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        ReviewScenario s;
        ExamSession session;
        string key;
        await using (var db = fx.CreateContext())
        {
            s = await ScenarioAsync(db);
            session = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky, events: new EventSpec(EventType.PHONE, 10, WithEvidence: true));
            var frame = await db.EvidenceFrames.SingleAsync(f => f.Event.ExamSessionId == session.Id);
            key = frame.StorageKey = (await _evidence.SaveAsync(BiometricTestHelpers.Jpeg())).StorageKey;
            await db.SaveChangesAsync();
        }

        var factory = Factory();
        var detail = await As(factory, s.Reviewer).GetFromJsonAsync<ReviewSessionDetail>($"/api/review/sessions/{session.Id}", Json);
        await _evidence.DeleteAsync(key); // bağlantı verildikten sonra dosya silindi (A5'teki saklama görevi gibi)

        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync(detail!.Events[0].EvidenceUrl)).StatusCode);
    }

    [SkippableFact]
    public async Task Corrupted_evidence_file_is_not_found_instead_of_a_server_error()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        ReviewScenario s;
        ExamSession session;
        await using (var db = fx.CreateContext())
        {
            s = await ScenarioAsync(db);
            session = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky,
                events: new EventSpec(EventType.PHONE, 10, WithEvidence: true));
        }

        var factory = new TestApi(fx).Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(x => x.AddSingleton<IEvidenceStore, TamperedEvidenceStore>()));
        var detail = await As(factory, s.Reviewer).GetFromJsonAsync<ReviewSessionDetail>($"/api/review/sessions/{session.Id}", Json);

        var response = await factory.CreateClient().GetAsync(detail!.Events[0].EvidenceUrl);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Assigned_reviewers_and_admins_are_notified_when_a_flagged_session_is_submitted()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        ReviewScenario s;
        ExamSession session;
        User candidate;
        await using (var db = fx.CreateContext())
        {
            s = await ScenarioAsync(db);
            candidate = TestData.Candidate();
            var now = DateTimeOffset.UtcNow;
            session = new ExamSession
            {
                ExamId = s.Exam.Id,
                Candidate = candidate,
                State = SessionState.InProgress,
                Flags = SessionFlags.Risky | SessionFlags.Indicated,
                StartedAt = now,
                EndsAt = now.AddMinutes(30),
                LastSeenAt = now,
            };
            db.ExamSessions.Add(session);
            await db.SaveChangesAsync();
        }

        var factory = Factory();
        var reviewerGot = new TaskCompletionSource<SessionReadyForReview>(TaskCreationOptions.RunContinuationsAsynchronously);
        var adminGot = new TaskCompletionSource<SessionReadyForReview>(TaskCreationOptions.RunContinuationsAsynchronously);
        var otherGot = new TaskCompletionSource<SessionReadyForReview>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var reviewer = await ConnectAsync(factory, s.Reviewer, reviewerGot);
        await using var admin = await ConnectAsync(factory, s.Admin, adminGot);
        await using var other = await ConnectAsync(factory, s.OtherReviewer, otherGot);

        var submit = await As(factory, candidate).PostAsync($"/api/sessions/{session.Id}/submit", null);

        Assert.True(submit.IsSuccessStatusCode);
        var message = await reviewerGot.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(session.Id, message.SessionId);
        Assert.Equal(s.Exam.Id, message.ExamId);
        Assert.Equal([SessionFlags.Risky, SessionFlags.Indicated], message.Flags);
        Assert.Equal(session.Id, (await adminGot.Task.WaitAsync(TimeSpan.FromSeconds(10))).SessionId);

        // Başka sınava atanmış değerlendirici bu oturumu duymuyor.
        await Task.Delay(300);
        Assert.False(otherGot.Task.IsCompleted);
    }

    [SkippableFact]
    public async Task Candidate_cannot_connect_to_the_review_hub()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        User candidate;
        await using (var db = fx.CreateContext())
        {
            candidate = (await TestData.SavedSessionAsync(db)).Candidate;
        }

        var factory = Factory();
        var connection = Hub(factory, Token(factory, candidate));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => connection.StartAsync());
        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }

    private static async Task<HubConnection> ConnectAsync(
        WebApplicationFactory<Program> factory, User user, TaskCompletionSource<SessionReadyForReview> received)
    {
        var connection = Hub(factory, Token(factory, user));
        connection.On<SessionReadyForReview>("SessionReadyForReview", m => received.TrySetResult(m));
        await connection.StartAsync();
        return connection;
    }

    /// <summary>
    /// Tarayıcı gibi belirteci access_token sorgu parametresiyle gönderiyor (WebSocket başlık taşıyamıyor).
    /// TestServer WebSocket'i istemci üzerinden desteklemediği için uzun yoklama (long polling) kullanılıyor;
    /// kimlik doğrulama yolu aynı.
    /// </summary>
    private static HubConnection Hub(WebApplicationFactory<Program> factory, string token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, $"/hubs/review?access_token={Uri.EscapeDataString(token)}"), o =>
            {
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
}

/// <summary>Dosyası değiştirilmiş depo: AES-GCM etiketi tutmadığında olduğu gibi çözme hatası veriyor.</summary>
internal sealed class TamperedEvidenceStore : IEvidenceStore
{
    public Task<StoredEvidence> SaveAsync(byte[] image, CancellationToken ct = default) => throw new NotSupportedException();

    public Task DeleteAsync(string storageKey, CancellationToken ct = default) => Task.CompletedTask;

    public Task<byte[]?> ReadAsync(string storageKey, int keyVersion, CancellationToken ct = default) =>
        throw new System.Security.Cryptography.AuthenticationTagMismatchException();
}
