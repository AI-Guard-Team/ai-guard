using System.Net;
using System.Net.Http.Json;
using AIGuard.Application.Auth;
using AIGuard.Application.Users;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.IntegrationTests.Auth;

/// <summary>E2 uçlarının HTTP üzerinden uçtan uca davranışı (kabul kriterleri).</summary>
public class AuthApiTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private const string Password = "Gecerli-Sifre-123";

    private TestApi? _api;
    private TestApi Api => _api ??= new TestApi(fx);

    private async Task<User> SavedUserAsync(UserRole role, string? password = Password)
    {
        var user = TestData.UserWithRole(role);
        if (password is not null)
        {
            user.PasswordHash = new Infrastructure.Security.IdentityPasswordHasher().Hash(password);
            user.InviteAcceptedAt = DateTimeOffset.UtcNow;
        }

        await using var db = fx.CreateContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<TokenResponse> LoginAsync(string email, string password = Password)
    {
        var response = await Api.Anonymous().PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    [SkippableFact]
    public async Task Invited_user_sets_password_and_logs_in()
    {
        // Kabul kriteri: davet edilen kullanıcı şifresini belirleyip giriş yapabilir.
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var admin = await SavedUserAsync(UserRole.Admin);
        var email = $"{Guid.NewGuid():N}@test.local";

        var create = await Api.As(admin).PostAsJsonAsync("/api/users", new { fullName = "Yeni Aday", email, role = "Candidate" });
        var token = Api.Emails.InviteTokenFor(email);
        var setPassword = await Api.Anonymous().PostAsJsonAsync("/api/auth/set-password", new { token, password = Password });
        var reuse = await Api.Anonymous().PostAsJsonAsync("/api/auth/set-password", new { token, password = Password });
        var tokens = await LoginAsync(email);

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, setPassword.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode); // davet tek kullanımlık
        Assert.False(string.IsNullOrEmpty(tokens.AccessToken));
        Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));
    }

    [SkippableFact]
    public async Task Unauthorized_role_gets_403_and_anonymous_gets_401()
    {
        // Kabul kriteri: yetkisi olmayan uca erişim 403 ile reddedilir.
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var candidate = await SavedUserAsync(UserRole.Candidate);

        var asCandidate = await Api.As(candidate).GetAsync("/api/users");
        var anonymous = await Api.Anonymous().GetAsync("/api/users");
        var health = await Api.Anonymous().GetAsync("/health");

        Assert.Equal(HttpStatusCode.Forbidden, asCandidate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode); // sağlık ucu açık kalmalı
    }

    [SkippableFact]
    public async Task Access_token_carries_the_claims_the_web_app_reads()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var reviewer = await SavedUserAsync(UserRole.Reviewer);

        var tokens = await LoginAsync(reviewer.Email);
        var payload = System.Text.Json.JsonDocument.Parse(Base64UrlDecode(tokens.AccessToken.Split('.')[1])).RootElement;

        Assert.Equal(reviewer.Id.ToString(), payload.GetProperty("sub").GetString());
        Assert.Equal("Reviewer", payload.GetProperty("role").GetString());
        Assert.Equal(reviewer.Email, payload.GetProperty("email").GetString());
        var lifetime = payload.GetProperty("exp").GetInt64() - payload.GetProperty("iat").GetInt64();
        Assert.Equal(15 * 60, lifetime);
    }

    [SkippableFact]
    public async Task Wrong_password_returns_401_with_a_turkish_message()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var user = await SavedUserAsync(UserRole.Candidate);

        var wrong = await Api.Anonymous().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "yanlis-sifre" });
        var unknown = await Api.Anonymous().PostAsJsonAsync("/api/auth/login", new { email = "yok@test.local", password = "yanlis-sifre" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        // Var olan ve olmayan e-posta için aynı yanıt (her istekte değişen traceId hariç).
        var wrongBody = await wrong.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        var unknownBody = await unknown.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.Equal(wrongBody!.Title, unknownBody!.Title);
        Assert.Equal(wrongBody.Detail, unknownBody.Detail);
        Assert.Equal("E-posta veya şifre hatalı.", wrongBody.Detail);
    }

    [SkippableFact]
    public async Task Five_failed_logins_lock_the_account()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var user = await SavedUserAsync(UserRole.Candidate);

        HttpResponseMessage last = null!;
        for (var i = 0; i < AuthPolicy.MaxFailedLogins; i++)
        {
            last = await Api.Anonymous().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "yanlis-sifre" });
        }

        // Kilitliyken doğru şifre de kabul edilmiyor.
        var correct = await Api.Anonymous().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = Password });

        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, correct.StatusCode);
    }

    [SkippableFact]
    public async Task Refresh_rotates_the_token_and_reuse_closes_all_sessions()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var user = await SavedUserAsync(UserRole.Candidate);
        var first = await LoginAsync(user.Email);

        var refreshed = await Api.Anonymous().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });
        var second = (await refreshed.Content.ReadFromJsonAsync<TokenResponse>())!;
        var reuse = await Api.Anonymous().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });
        // Eski belirteç tekrar kullanıldığı için yeni belirteç de iptal edildi.
        var afterReuse = await Api.Anonymous().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = second.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [SkippableFact]
    public async Task Logout_revokes_the_refresh_token()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var user = await SavedUserAsync(UserRole.Candidate);
        var tokens = await LoginAsync(user.Email);

        var logout = await Api.Anonymous().PostAsJsonAsync("/api/auth/logout", new { refreshToken = tokens.RefreshToken });
        var refresh = await Api.Anonymous().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [SkippableFact]
    public async Task Deactivated_or_role_changed_users_token_is_rejected_on_next_request()
    {
        // FR-02 ve kart: pasife alınan kullanıcının belirteçleri bir sonraki istekte reddedilmeli.
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var admin = await SavedUserAsync(UserRole.Admin);
        var target = await SavedUserAsync(UserRole.Admin);
        var reviewer = await SavedUserAsync(UserRole.Reviewer);
        var targetClient = Api.WithToken((await LoginAsync(target.Email)).AccessToken);
        var reviewerTokens = await LoginAsync(reviewer.Email);
        var reviewerClient = Api.WithToken(reviewerTokens.AccessToken);

        var before = await targetClient.GetAsync("/api/users");
        await Api.As(admin).PatchAsJsonAsync($"/api/users/{target.Id}", new { isActive = false });
        await Api.As(admin).PatchAsJsonAsync($"/api/users/{reviewer.Id}", new { role = "Candidate" });
        var afterDeactivate = await targetClient.GetAsync("/api/users");
        var afterRoleChange = await reviewerClient.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = Password, newPassword = "Baska-Sifre-456" });

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterDeactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterRoleChange.StatusCode);

        await using var db = fx.CreateContext();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.TargetId == target.Id && a.Action == "user.deactivate"));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.TargetId == reviewer.Id && a.Action == "user.role_change"));
    }

    [SkippableFact]
    public async Task Admin_lists_users_without_secrets_and_cannot_change_self()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var admin = await SavedUserAsync(UserRole.Admin);
        await SavedUserAsync(UserRole.Candidate);

        var list = await Api.As(admin).GetAsync("/api/users?role=Candidate");
        var json = await list.Content.ReadAsStringAsync();
        // API enum'ları metin olarak döndürüyor (K-05); okurken de aynı ayar gerekiyor.
        var jsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        var users = await list.Content.ReadFromJsonAsync<List<UserSummary>>(jsonOptions);
        var self = await Api.As(admin).PatchAsJsonAsync($"/api/users/{admin.Id}", new { isActive = false });

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.All(users!, u => Assert.Equal(UserRole.Candidate, u.Role));
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
    }

    [SkippableFact]
    public async Task Duplicate_email_returns_409_and_invalid_input_400()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var admin = await SavedUserAsync(UserRole.Admin);
        var existing = await SavedUserAsync(UserRole.Candidate);

        // Büyük harf farkı aynı adres sayılıyor.
        var duplicate = await Api.As(admin).PostAsJsonAsync("/api/users",
            new { fullName = "Kopya", email = existing.Email.ToUpperInvariant(), role = "Candidate" });
        var badEmail = await Api.As(admin).PostAsJsonAsync("/api/users", new { fullName = "X", email = "adres-degil", role = "Candidate" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);
    }

    [SkippableFact]
    public async Task Change_password_requires_current_password_and_closes_other_sessions()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var user = await SavedUserAsync(UserRole.Candidate);
        var tokens = await LoginAsync(user.Email);
        var client = Api.WithToken(tokens.AccessToken);

        var wrong = await client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "yanlis", newPassword = "Yeni-Sifre-789" });
        var weak = await client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = Password, newPassword = "kisa" });
        var ok = await client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = Password, newPassword = "Yeni-Sifre-789" });
        var oldRefresh = await Api.Anonymous().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken });
        var newLogin = await Api.Anonymous().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Yeni-Sifre-789" });

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode); // 401 değil: önyüz oturumu kapatmasın
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [SkippableFact]
    public async Task Forgotten_password_is_reset_with_a_single_use_link()
    {
        // Şifremi unuttum: bağlantı gider, yeni şifre belirlenir, eski oturumlar kapanır, bağlantı ikinci kez çalışmaz.
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var user = await SavedUserAsync(UserRole.Candidate);
        var tokens = await LoginAsync(user.Email);

        var forgot = await Api.Anonymous().PostAsJsonAsync("/api/auth/forgot-password", new { email = user.Email.ToUpperInvariant() });
        var token = Assert.Single(Api.Emails.ResetTokensFor(user.Email));
        var weak = await Api.Anonymous().PostAsJsonAsync("/api/auth/reset-password", new { token, password = "kisa" });
        var reset = await Api.Anonymous().PostAsJsonAsync("/api/auth/reset-password", new { token, password = "Yeni-Sifre-789" });
        var reuse = await Api.Anonymous().PostAsJsonAsync("/api/auth/reset-password", new { token, password = "Baska-Sifre-456" });
        var oldRefresh = await Api.Anonymous().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken });
        var oldLogin = await Api.Anonymous().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = Password });
        var newLogin = await Api.Anonymous().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Yeni-Sifre-789" });

        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode); // kurala uymayan şifre bağlantıyı harcamaz
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [SkippableFact]
    public async Task Forgot_password_does_not_reveal_whether_the_email_exists()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var invited = TestData.UserWithRole(UserRole.Candidate); // daveti kabul etmemiş: şifresi yok
        await using (var db = fx.CreateContext())
        {
            db.Users.Add(invited);
            await db.SaveChangesAsync();
        }

        var unknown = await Api.Anonymous().PostAsJsonAsync("/api/auth/forgot-password", new { email = $"{Guid.NewGuid():N}@test.local" });
        var notAccepted = await Api.Anonymous().PostAsJsonAsync("/api/auth/forgot-password", new { email = invited.Email });
        var empty = await Api.Anonymous().PostAsJsonAsync("/api/auth/forgot-password", new { email = "" });
        var badToken = await Api.Anonymous().PostAsJsonAsync("/api/auth/reset-password", new { token = "yok", password = "Yeni-Sifre-789" });

        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, notAccepted.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, empty.StatusCode);
        Assert.Empty(Api.Emails.ResetTokensFor(invited.Email)); // davet bekleyen hesaba sıfırlama gitmez
        Assert.Equal(HttpStatusCode.BadRequest, badToken.StatusCode);
    }

    [SkippableFact]
    public async Task There_is_no_self_registration_endpoint()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

        var response = await Api.Anonymous().PostAsJsonAsync("/api/users", new { fullName = "Ben", email = "ben@test.local", role = "Admin" });
        var register = await Api.Anonymous().PostAsJsonAsync("/api/auth/register", new { email = "ben@test.local" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(register.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized);
    }

    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + ((4 - (s.Length % 4)) % 4), '='));
    }
}
