using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIGuard.Api.Controllers;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Agents;
using AIGuard.Application.Auth;
using AIGuard.Application.Sessions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.IntegrationTests.Biometrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AIGuard.IntegrationTests.E2E;

/// <summary>
/// 1. entegrasyon kontrol noktası (E5a): temel akış uçtan uca, yalnızca HTTP üzerinden.
/// davet → şifre → giriş → kayıt fotoğrafı → sınava başla → rıza → ajan eşleştirme → ajan sinyali
/// → kimlik doğrulama → sınav → teslim.
/// </summary>
/// <remarks>
/// Her adım ekibin gerçek uçlarını çağırıyor (E2, E3, E4, A3, Y1, Y2). Yalnızca iki dış bağımlılık sahte:
/// görüntü servisi (Python, ayrı süreç) ve kanıt karesi dosya deposu. Ajanın yaptığını test yapıyor:
/// kodu /api/agent/pair'e iletip dönen oturum anahtarıyla imzalı sinyal gönderiyor. CI'da her PR'da çalışıyor.
/// Bir adım bozulursa test o adımın adıyla düşüyor (<see cref="Step"/>).
/// </remarks>
public class CandidateFlowE2ETests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private const string Password = "E2e-Aday-Sifresi-1";

    private readonly FakeFaceService _vision = new();
    private TestApi? _api;

    private TestApi Api => _api ??= new TestApi(fx, services: s =>
    {
        s.AddSingleton<IFaceService>(_vision);
        s.AddSingleton<IEvidenceStore>(new MemoryEvidenceStore());
    });

    private sealed record StateBody(SessionState State);

    private sealed record PairBody(Guid PairingId, Guid SessionId, string SessionKey);

    private sealed record CodeBody(string Code);

    /// <summary>Adımı çalıştırır; başarısız olursa hangi adımda kalındığı ve yanıt gövdesi mesajda görünür.</summary>
    private static async Task<HttpResponseMessage> Step(string name, HttpStatusCode expected, Task<HttpResponseMessage> call)
    {
        var response = await call;
        Assert.True(response.StatusCode == expected,
            $"E2E adımı \"{name}\": beklenen {(int)expected}, gelen {(int)response.StatusCode}. Yanıt: {await response.Content.ReadAsStringAsync()}");
        return response;
    }

    private static async Task<T> Body<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    [SkippableFact]
    public async Task Candidate_goes_from_invitation_to_submitted_exam()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

        // Hazırlık: sistemde bir yönetici var (örnek veri ya da kurulum).
        var adminUser = TestData.UserWithRole(UserRole.Admin);
        await using (var db = fx.CreateContext())
        {
            db.Users.Add(adminUser);
            await db.SaveChangesAsync();
        }

        var admin = Api.As(adminUser);
        var email = $"e2e-{Guid.NewGuid():N}@test.local";

        // 1. Davet (E2): yönetici adayı oluşturur, e-postaya tek kullanımlık bağlantı gider.
        var created = await Body<CreatedUserResponse>(await Step("davet", HttpStatusCode.Created,
            admin.PostAsJsonAsync("/api/users", new { fullName = "E2E Aday", email, role = "Candidate" })));

        // 2. Şifre belirleme ve giriş (E2).
        await Step("şifre belirleme", HttpStatusCode.NoContent, Api.Anonymous().PostAsJsonAsync("/api/auth/set-password",
            new { token = Api.Emails.InviteTokenFor(email), password = Password }));
        var tokens = await Body<TokenResponse>(await Step("giriş", HttpStatusCode.OK,
            Api.Anonymous().PostAsJsonAsync("/api/auth/login", new { email, password = Password })));
        var candidate = Api.WithToken(tokens.AccessToken);

        // 3. Sınav hazırlığı (E3): yönetici sınavı, soruyu ve atamayı oluşturur.
        var exam = await Body<CreatedResponse>(await Step("sınav oluşturma", HttpStatusCode.Created, admin.PostAsJsonAsync("/api/exams", new
        {
            name = "E2E Genel Yetenek",
            type = "GeneralAptitude",
            durationMinutes = 30,
            windowStartAt = DateTimeOffset.UtcNow.AddHours(-1),
            windowEndAt = DateTimeOffset.UtcNow.AddDays(1),
        })));
        await Step("soru ekleme", HttpStatusCode.Created, admin.PostAsJsonAsync($"/api/exams/{exam.Id}/questions",
            new { text = "2, 4, 8, 16, ... sıradaki?", options = new[] { "24", "32", "30" }, correctOptionIndex = 1 }));
        await Step("aday atama", HttpStatusCode.Created, admin.PostAsJsonAsync($"/api/exams/{exam.Id}/assignments",
            new { userId = created.Id, role = "Candidate" }));

        // 4. Kayıt fotoğrafı (A3): ayrı biyometrik rızayla.
        using (var photo = new MultipartFormDataContent())
        {
            var file = new ByteArrayContent(BiometricTestHelpers.Jpeg());
            file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            photo.Add(file, "photo", "kayit.jpg");
            photo.Add(new StringContent("true"), "consent");
            photo.Add(new StringContent(ConsentText.InitialBiometricVersion), "consentTextVersion");
            var upload = await candidate.PostAsync("/api/me/photo", photo);
            Assert.True(upload.IsSuccessStatusCode, $"E2E adımı \"kayıt fotoğrafı\": {(int)upload.StatusCode} {await upload.Content.ReadAsStringAsync()}");
        }

        // 5. Sınava başla (E4): oturum ConsentPending.
        var session = await Body<StartSessionResponse>(await Step("sınava başla", HttpStatusCode.Created,
            candidate.PostAsync($"/api/exams/{exam.Id}/sessions", null)));
        Assert.Equal(SessionState.ConsentPending, session.State);
        var sessionUrl = $"/api/sessions/{session.SessionId}";

        // 6. Oturum rızası (A3): AgentPairing.
        var consent = await Body<StateBody>(await Step("rıza", HttpStatusCode.OK, candidate.PostAsJsonAsync($"{sessionUrl}/consent",
            new { accepted = true, textVersion = ConsentText.InitialSessionVersion })));
        Assert.Equal(SessionState.AgentPairing, consent.State);

        // 7. Ajan eşleştirme (Y1): tarayıcı kodu alır; ajan (burada test) kodu sunucuya iletir, oturum anahtarını alır.
        var code = await Body<CodeBody>(await Step("eşleştirme kodu", HttpStatusCode.Created, candidate.PostAsync($"{sessionUrl}/agent-code", null)));
        var pair = await Body<PairBody>(await Step("ajan eşleştirme", HttpStatusCode.OK,
            Api.Anonymous().PostAsJsonAsync("/api/agent/pair", new { code = code.Code, os = "Windows" })));
        Assert.Equal(session.SessionId, pair.SessionId);

        // 8. İlk imzalı ajan sinyali (Y2): gösterge yok → kimlik doğrulama adımı.
        var key = Convert.FromBase64String(pair.SessionKey);
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = Agents.PairedAgent.NewNonce();
        AgentIndicator[] indicators = [];
        await Step("ajan sinyali", HttpStatusCode.Accepted, Api.Anonymous().PostAsJsonAsync("/api/agent/signal", new
        {
            sessionId = session.SessionId, ts, nonce, indicators,
            sig = AgentSignature.Sign(key, session.SessionId, ts, nonce, indicators),
        }));

        // 9. Kimlik doğrulama (A3, D2 sözleşmesi): canlılık ve eşleşme başarılı → sınav başlar, süre işler.
        using (var frames = new MultipartFormDataContent())
        {
            foreach (var (frame, i) in BiometricTestHelpers.Frames().Select((f, i) => (f, i)))
            {
                var part = new ByteArrayContent(frame);
                part.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                frames.Add(part, "frames", $"kare-{i + 1}.jpg");
            }

            var verified = await Body<StateBody>(await Step("kimlik doğrulama", HttpStatusCode.OK, candidate.PostAsync($"{sessionUrl}/verify", frames)));
            Assert.Equal(SessionState.InProgress, verified.State);
        }

        // 10. Sınav (E4): sorular doğru seçeneksiz gelir; cevap sıra numarasıyla kaydedilir.
        var view = await Body<SessionView>(await Step("sınav ekranı", HttpStatusCode.OK, candidate.GetAsync(sessionUrl)));
        Assert.NotNull(view.EndsAt);
        var question = Assert.Single(view.Questions);
        await Step("cevap", HttpStatusCode.Created, candidate.PostAsJsonAsync($"{sessionUrl}/answers",
            new { questionId = question.Id, selectedOption = 1, seq = 1, clientTime = DateTimeOffset.UtcNow }));

        // 11. Teslim (E4).
        await Step("teslim", HttpStatusCode.NoContent, candidate.PostAsync($"{sessionUrl}/submit", null));

        // Sonuç: veritabanında akışın bütün izleri olmalı.
        await using var check = fx.CreateContext();
        var stored = await check.ExamSessions.AsNoTracking()
            .Include(s => s.Answers).Include(s => s.ConsentRecords).Include(s => s.Verifications).Include(s => s.AgentPairing)
            .SingleAsync(s => s.Id == session.SessionId);
        var assignment = await check.ExamAssignments.AsNoTracking().SingleAsync(a => a.ExamId == exam.Id && a.UserId == created.Id);

        Assert.Equal(SessionState.Completed, stored.State);
        Assert.NotNull(stored.EndedAt);
        Assert.Equal(1, Assert.Single(stored.Answers).SelectedOption);
        Assert.Contains(stored.ConsentRecords, c => c.Type == ConsentType.Session);
        Assert.Contains(stored.Verifications, v => v.Kind == VerificationKind.Initial);
        Assert.Equal(AgentPlatform.Windows, stored.AgentPairing!.Platform);
        Assert.NotNull(assignment.AttemptUsedAt); // tek oturum hakkı kullanıldı
        Assert.True(await check.ReferenceFaceTemplates.AnyAsync(t => t.UserId == created.Id)); // yalnızca şifreli vektör
        Assert.True(await check.AuditLogs.AnyAsync(a => a.TargetId == created.Id && a.Action == "user.login"));
        Assert.Equal(2, _vision.Calls); // kayıt + doğrulama; görüntü servisi başka yerden çağrılmadı

        // Tek oturum hakkı: aynı sınava ikinci kez başlanamaz.
        await Step("ikinci başlatma reddi", HttpStatusCode.Conflict, candidate.PostAsync($"/api/exams/{exam.Id}/sessions", null));
    }
}
