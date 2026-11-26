using System.Net;
using System.Net.Http.Json;
using AIGuard.Application.Risk;
using AIGuard.Application.Settings;
using AIGuard.Application.Users;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.IntegrationTests.Management;

/// <summary>K-48 (sunucu metinlerinin dili) ve K-49 (yönetim uçları) HTTP üzerinden.</summary>
public class ManagementApiTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private const string Password = "Gecerli-Sifre-123";

    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private TestApi? _api;
    private TestApi Api => _api ??= new TestApi(fx);

    private async Task<User> SavedUserAsync(UserRole role, bool accepted = true)
    {
        var user = TestData.UserWithRole(role);
        if (accepted)
        {
            user.PasswordHash = new Infrastructure.Security.IdentityPasswordHasher().Hash(Password);
            user.InviteAcceptedAt = DateTimeOffset.UtcNow;
        }

        await using var db = fx.CreateContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [SkippableFact]
    public async Task Problem_text_follows_accept_language()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var user = await SavedUserAsync(UserRole.Candidate);

        async Task<ProblemDetails> LoginAsync(string? language)
        {
            var client = Api.Anonymous();
            if (language is not null) client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "yanlis-sifre" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;
        }

        var english = await LoginAsync("en-US,en;q=0.9");
        var turkish = await LoginAsync("tr-TR");
        var none = await LoginAsync(null);
        var preferTurkish = await LoginAsync("tr;q=0.9,en;q=0.5");

        Assert.Equal("Sign-in failed.", english.Title);
        Assert.Equal("Incorrect email or password.", english.Detail);
        Assert.Equal("Giriş başarısız.", turkish.Title);
        Assert.Equal("E-posta veya şifre hatalı.", turkish.Detail);
        Assert.Equal("Giriş başarısız.", none.Title); // varsayılan Türkçe
        Assert.Equal("Giriş başarısız.", preferTurkish.Title);
    }

    [SkippableFact]
    public async Task Login_records_last_login_and_admin_sees_it()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var admin = await SavedUserAsync(UserRole.Admin);
        var candidate = await SavedUserAsync(UserRole.Candidate);

        var login = await Api.Anonymous().PostAsJsonAsync("/api/auth/login", new { email = candidate.Email, password = Password });
        var users = await Api.As(admin).GetFromJsonAsync<List<UserSummary>>("/api/users", Json);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var row = Assert.Single(users!, u => u.Id == candidate.Id);
        Assert.NotNull(row.LastLoginAt);
        Assert.True(DateTimeOffset.UtcNow - row.LastLoginAt!.Value < TimeSpan.FromMinutes(1));
    }

    [SkippableFact]
    public async Task Invite_resend_issues_new_link_kills_old_and_is_rate_limited()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var admin = await SavedUserAsync(UserRole.Admin);
        var email = $"{Guid.NewGuid():N}@test.local";
        var create = await Api.As(admin).PostAsJsonAsync("/api/users", new { fullName = "Davetli", email, role = "Candidate" });
        var userId = (await create.Content.ReadFromJsonAsync<CreatedUser>())!.Id;
        var firstToken = Api.Emails.InviteTokenFor(email);

        // Hemen yeniden gönderme: dakikada bir sınırı.
        var tooSoon = await Api.As(admin).PostAsync($"/api/users/{userId}/invite", null);
        Assert.Equal(HttpStatusCode.TooManyRequests, tooSoon.StatusCode);

        // Bekleme süresi geçmiş gibi.
        await using (var db = fx.CreateContext())
        {
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            user.InviteSentAt = DateTimeOffset.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        var resend = await Api.As(admin).PostAsync($"/api/users/{userId}/invite", null);
        var secondToken = Api.Emails.InviteTokenFor(email);
        var oldLink = await Api.Anonymous().PostAsJsonAsync("/api/auth/set-password", new { token = firstToken, password = Password });
        var newLink = await Api.Anonymous().PostAsJsonAsync("/api/auth/set-password", new { token = secondToken, password = Password });
        var afterAccept = await Api.As(admin).PostAsync($"/api/users/{userId}/invite", null);

        Assert.Equal(HttpStatusCode.NoContent, resend.StatusCode);
        Assert.NotEqual(firstToken, secondToken);
        Assert.Equal(HttpStatusCode.BadRequest, oldLink.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, newLink.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, afterAccept.StatusCode);

        await using var check = fx.CreateContext();
        Assert.True(await check.AuditLogs.AnyAsync(a => a.Action == "user.invite_resend" && a.TargetId == userId));
    }

    [SkippableFact]
    public async Task Platform_settings_are_read_by_everyone_and_written_by_admin_only()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var admin = await SavedUserAsync(UserRole.Admin);
        var candidate = await SavedUserAsync(UserRole.Candidate);

        var forbidden = await Api.As(candidate).PutAsJsonAsync("/api/platform-settings", new { supportEmail = "x@test.local" });
        var unsafeUrl = await Api.As(admin).PutAsJsonAsync("/api/platform-settings", new { agentDownloadUrlWindows = "javascript:alert(1)" });
        var plainHttp = await Api.As(admin).PutAsJsonAsync("/api/platform-settings", new { agentDownloadUrlWindows = "http://kurum.edu.tr/ajan.exe" });
        var badPhone = await Api.As(admin).PutAsJsonAsync("/api/platform-settings", new { supportPhone = "<script>" });
        var saved = await Api.As(admin).PutAsJsonAsync("/api/platform-settings", new
        {
            alternativeMethodText = "  Gözetmenli salon sınavı için koordinatörlüğe başvurun.  ",
            supportEmail = "sinav-destek@kurum.edu.tr",
            supportPhone = "+90 (212) 555 0190",
            agentDownloadUrlWindows = "https://kurum.edu.tr/aiguard-agent.exe",
            agentDownloadUrlMac = "",
        });
        var read = await Api.As(candidate).GetFromJsonAsync<PlatformSettingsView>("/api/platform-settings", Json);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unsafeUrl.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, plainHttp.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badPhone.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        Assert.Equal("Gözetmenli salon sınavı için koordinatörlüğe başvurun.", read!.AlternativeMethodText);
        Assert.Equal("https://kurum.edu.tr/aiguard-agent.exe", read.AgentDownloadUrlWindows);
        Assert.Null(read.AgentDownloadUrlMac); // boş metin "ayarlanmadı"

        await using var db = fx.CreateContext();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "settings.update" && a.ActorUserId == admin.Id));
    }

    [SkippableFact]
    public async Task Exam_risk_settings_override_reset_and_lock_after_first_session()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var admin = await SavedUserAsync(UserRole.Admin);
        var exam = TestData.Exam();
        await using (var db = fx.CreateContext())
        {
            db.Exams.Add(exam);
            await db.SaveChangesAsync();
        }

        var client = Api.As(admin);
        var url = $"/api/exams/{exam.Id}/risk-settings";
        var defaults = await client.GetFromJsonAsync<ExamRiskSettingsView>(url, Json);
        var phone = defaults!.Events.Single(e => e.Type == EventType.PHONE);

        var invalid = await client.PutAsJsonAsync(url, new { events = new[] { new { type = "PHONE", weight = 500, decayPerMinute = 0, threshold = (double?)null } } });
        var update = await client.PutAsJsonAsync(url, new
        {
            sessionThreshold = defaults.DefaultSessionThreshold + 5,
            events = new[] { new { type = "PHONE", weight = phone.DefaultWeight + 3, decayPerMinute = 0.1, threshold = (double?)40 } },
        });
        var changed = await client.GetFromJsonAsync<ExamRiskSettingsView>(url, Json);

        Assert.False(defaults.Locked);
        Assert.False(phone.Customized);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        Assert.Equal(defaults.DefaultSessionThreshold + 5, changed!.SessionThreshold);
        var changedPhone = changed.Events.Single(e => e.Type == EventType.PHONE);
        Assert.True(changedPhone.Customized);
        Assert.Equal(phone.DefaultWeight + 3, changedPhone.Weight);
        Assert.Equal(40, changedPhone.Threshold);
        Assert.False(changed.Events.Single(e => e.Type == EventType.TAB_BLUR).Customized);

        // Oturum açılınca kilit: skorlar adaylar arasında tutarlı kalmalı.
        await using (var db = fx.CreateContext())
        {
            db.ExamSessions.Add(new ExamSession { ExamId = exam.Id, Candidate = TestData.Candidate() });
            await db.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        var locked = await client.DeleteAsync(url);
        var lockedProblem = await locked.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Equal("Sessions have started in this exam; its risk settings can no longer be changed.", lockedProblem!.Detail);
    }

    private sealed record CreatedUser(Guid Id);
}
