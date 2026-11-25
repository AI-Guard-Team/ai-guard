using AIGuard.Application.Abstractions;
using AIGuard.Application.Auth;
using AIGuard.Application.Users;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Persistence;
using AIGuard.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;

namespace AIGuard.IntegrationTests.Auth;

/// <summary>Süreye bağlı kurallar: saat FakeTimeProvider ile ileri alınıyor, beklenmiyor (K-15).</summary>
public class AuthServiceTimeTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private const string Password = "Gecerli-Sifre-123";
    private static readonly IPasswordHasher Hasher = new IdentityPasswordHasher();
    private readonly CapturingEmailSender Emails = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero));

    private AuthService Auth(AppDbContext db)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["JWT_SECRET"] = TestApi.JwtSecret }).Build();
        return new AuthService(db, Hasher, new JwtTokenService(JwtSettings.FromConfiguration(config), _clock), Emails, _clock);
    }

    private async Task<User> SavedUserAsync()
    {
        var user = TestData.UserWithRole(UserRole.Candidate);
        user.PasswordHash = Hasher.Hash(Password);
        await using var db = fx.CreateContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [SkippableFact]
    public async Task Lockout_ends_after_15_minutes()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var user = await SavedUserAsync();
        await using var db = fx.CreateContext();
        var auth = Auth(db);

        for (var i = 0; i < AuthPolicy.MaxFailedLogins; i++)
        {
            await auth.LoginAsync(new LoginRequest(user.Email, "yanlis-sifre"));
        }

        _clock.Advance(TimeSpan.FromMinutes(14));
        var stillLocked = await auth.LoginAsync(new LoginRequest(user.Email, Password));
        _clock.Advance(TimeSpan.FromMinutes(2));
        var unlocked = await auth.LoginAsync(new LoginRequest(user.Email, Password));

        Assert.Equal(LoginStatus.LockedOut, stillLocked.Status);
        Assert.Equal(LoginStatus.Success, unlocked.Status);
    }

    [SkippableFact]
    public async Task Successful_login_resets_the_failure_counter()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var user = await SavedUserAsync();
        await using var db = fx.CreateContext();
        var auth = Auth(db);

        for (var i = 0; i < AuthPolicy.MaxFailedLogins - 1; i++)
        {
            await auth.LoginAsync(new LoginRequest(user.Email, "yanlis-sifre"));
        }

        await auth.LoginAsync(new LoginRequest(user.Email, Password));
        var afterReset = await auth.LoginAsync(new LoginRequest(user.Email, "yanlis-sifre"));

        Assert.Equal(LoginStatus.InvalidCredentials, afterReset.Status);
    }

    [SkippableFact]
    public async Task Refresh_token_expires_after_7_days()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var user = await SavedUserAsync();
        await using var db = fx.CreateContext();
        var auth = Auth(db);
        var login = await auth.LoginAsync(new LoginRequest(user.Email, Password));

        _clock.Advance(AuthPolicy.RefreshTokenLifetime + TimeSpan.FromSeconds(1));
        var result = await auth.RefreshAsync(new RefreshRequest(login.Tokens!.RefreshToken));

        Assert.Equal(AuthOperationStatus.InvalidToken, result.Status);
    }

    [SkippableFact]
    public async Task Invite_expires_after_72_hours()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var emails = new CapturingEmailSender();
        var admin = TestData.UserWithRole(UserRole.Admin);
        await using var db = fx.CreateContext();
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        var email = $"{Guid.NewGuid():N}@test.local";

        await new UserService(db, emails, _clock).CreateAsync(admin.Id, new CreateUserRequest("Aday", email, UserRole.Candidate));
        _clock.Advance(AuthPolicy.InviteLifetime + TimeSpan.FromSeconds(1));
        var result = await Auth(db).SetPasswordAsync(new SetPasswordRequest(emails.InviteTokenFor(email), Password));

        Assert.Equal(AuthOperationStatus.InvalidToken, result.Status);
    }

    [SkippableFact]
    public async Task Reset_link_expires_after_30_minutes_and_is_not_resent_within_2_minutes()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var user = await SavedUserAsync();
        await using var db = fx.CreateContext();
        var auth = Auth(db);

        await auth.RequestPasswordResetAsync(new ForgotPasswordRequest(user.Email));
        await auth.RequestPasswordResetAsync(new ForgotPasswordRequest(user.Email)); // bekleme süresinde: gönderilmez
        _clock.Advance(AuthPolicy.PasswordResetCooldown);
        await auth.RequestPasswordResetAsync(new ForgotPasswordRequest(user.Email)); // yeni bağlantı, eskisi geçersiz
        var sent = Emails.ResetTokensFor(user.Email);
        var oldLink = await auth.ResetPasswordAsync(new ResetPasswordRequest(sent[0], "Yeni-Sifre-789"));
        _clock.Advance(AuthPolicy.PasswordResetLifetime + TimeSpan.FromSeconds(1));
        var expired = await auth.ResetPasswordAsync(new ResetPasswordRequest(sent[1], "Yeni-Sifre-789"));

        Assert.Equal(2, sent.Count);
        Assert.Equal(AuthOperationStatus.InvalidToken, oldLink.Status);
        Assert.Equal(AuthOperationStatus.InvalidToken, expired.Status);
    }

    [SkippableFact]
    public async Task Password_reset_lifts_the_lockout()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var user = await SavedUserAsync();
        await using var db = fx.CreateContext();
        var auth = Auth(db);
        for (var i = 0; i < AuthPolicy.MaxFailedLogins; i++)
        {
            await auth.LoginAsync(new LoginRequest(user.Email, "yanlis-sifre"));
        }

        await auth.RequestPasswordResetAsync(new ForgotPasswordRequest(user.Email));
        var reset = await auth.ResetPasswordAsync(new ResetPasswordRequest(Emails.ResetTokensFor(user.Email)[0], "Yeni-Sifre-789"));
        var login = await auth.LoginAsync(new LoginRequest(user.Email, "Yeni-Sifre-789"));

        Assert.Equal(AuthOperationStatus.Success, reset.Status);
        Assert.Equal(LoginStatus.Success, login.Status);
    }

    [SkippableFact]
    public async Task Inactive_user_cannot_log_in_and_gets_the_same_answer_as_wrong_password()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var user = await SavedUserAsync();
        await using var db = fx.CreateContext();
        (await db.Users.FindAsync(user.Id))!.IsActive = false;
        await db.SaveChangesAsync();

        var result = await Auth(db).LoginAsync(new LoginRequest(user.Email, Password));

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
    }
}
