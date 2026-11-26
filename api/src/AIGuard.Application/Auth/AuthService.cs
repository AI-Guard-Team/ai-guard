using AIGuard.Application.Abstractions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Auth;

/// <summary>
/// Giriş, belirteç yenileme, çıkış, davetle şifre belirleme ve şifre değiştirme (E2, FR-01, FR-02).
/// </summary>
/// <remarks>Kararların gerekçeleri: MIMARI-KARARLAR K-20.</remarks>
public sealed class AuthService(IAppDbContext db, IPasswordHasher hasher, ITokenService tokens, IEmailSender email, TimeProvider clock)
{
    // Var olmayan e-postayla gelen girişte de bir özet doğrulanıyor; yanıt süresinden
    // e-postanın kayıtlı olup olmadığı anlaşılmasın. Sınıf başına bir kez üretiliyor.
    private static string? _dummyHash;

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password)
            || request.Password.Length > AuthPolicy.MaxPasswordLength)
        {
            return new(LoginStatus.InvalidCredentials);
        }

        var now = clock.GetUtcNow();
        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == email, ct);

        if (user is null || user.PasswordHash is null || !user.IsActive)
        {
            hasher.Verify(_dummyHash ??= hasher.Hash("aiguard-dummy-password"), request.Password);
            return new(LoginStatus.InvalidCredentials);
        }

        // Kilitliyken şifre denenmiyor bile; kilit süresince kaba kuvvet ilerleyemesin.
        if (user.LockoutEndAt > now)
        {
            return new(LoginStatus.LockedOut, LockoutEndAt: user.LockoutEndAt);
        }

        if (!hasher.Verify(user.PasswordHash, request.Password))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= AuthPolicy.MaxFailedLogins)
            {
                // Sayaç sıfırlanıyor: kilit bitince kullanıcı yine 5 hak alıyor.
                user.FailedLoginCount = 0;
                user.LockoutEndAt = now + AuthPolicy.LockoutDuration;
                Audit(user.Id, "user.lockout", user.Id, now);
                await db.SaveChangesAsync(ct);
                return new(LoginStatus.LockedOut, LockoutEndAt: user.LockoutEndAt);
            }

            await db.SaveChangesAsync(ct);
            return new(LoginStatus.InvalidCredentials);
        }

        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        user.LastLoginAt = now; // K-49: yönetici kullanıcı listesinde son giriş
        var response = IssueTokens(user, now);
        Audit(user.Id, "user.login", user.Id, now); // FR-35: giriş denetim kaydına
        await db.SaveChangesAsync(ct);
        return new(LoginStatus.Success, response);
    }

    /// <summary>
    /// Yenileme belirtecini tek kullanımlık olarak yenisiyle değiştirir (rotation).
    /// Daha önce yenilenmiş bir belirteç tekrar gelirse belirteç çalınmış sayılıp kullanıcının
    /// bütün oturumları kapatılıyor.
    /// </summary>
    public async Task<AuthOperationResult> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        if (!SecureToken.IsWellFormed(request.RefreshToken))
        {
            return new(AuthOperationStatus.InvalidToken);
        }

        var now = clock.GetUtcNow();
        var hash = SecureToken.Hash(request.RefreshToken!);
        var stored = await db.RefreshTokens.Include(t => t.User).SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null)
        {
            return new(AuthOperationStatus.InvalidToken);
        }

        if (stored.RevokedAt is not null)
        {
            if (stored.ReplacedByTokenId is not null)
            {
                // Yenilenmiş belirteç ikinci kez kullanıldı: ya saldırgan ya asıl kullanıcı
                // eski kopyayı tutuyor. Hangisi olduğu bilinemediği için zincirin tamamı kapanıyor.
                await RevokeAllAsync(stored.UserId, now, ct);
                Audit(stored.UserId, "auth.refresh_reuse", stored.UserId, now);
                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Belirteçlerden biri aynı anda değişti; istek zaten reddediliyor.
                }
            }

            return new(AuthOperationStatus.InvalidToken);
        }

        if (stored.ExpiresAt <= now || !stored.User.IsActive)
        {
            return new(AuthOperationStatus.InvalidToken);
        }

        var response = IssueTokens(stored.User, now, out var replacement);
        stored.RevokedAt = now;
        stored.ReplacedByTokenId = replacement.Id;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Aynı belirteçle eşzamanlı başka bir yenileme önce bitti (xmin). Bu istek reddediliyor;
            // tek kullanım kuralı korunuyor.
            return new(AuthOperationStatus.InvalidToken);
        }

        return new(AuthOperationStatus.Success, Tokens: response);
    }

    /// <summary>Belirteci geçersiz kılar. Belirteç yoksa da başarılı sayılıyor; çıkış her durumda tamamlanmalı.</summary>
    public async Task LogoutAsync(RefreshRequest request, CancellationToken ct = default)
    {
        if (!SecureToken.IsWellFormed(request.RefreshToken))
        {
            return;
        }

        var hash = SecureToken.Hash(request.RefreshToken!);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null, ct);
        if (stored is null)
        {
            return;
        }

        stored.RevokedAt = clock.GetUtcNow();
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Aynı anda yenilendi; belirteç zaten kullanılmış durumda.
        }
    }

    /// <summary>Davet bağlantısındaki belirteçle ilk şifreyi belirler; davet tek kullanımlık.</summary>
    public async Task<AuthOperationResult> SetPasswordAsync(SetPasswordRequest request, CancellationToken ct = default)
    {
        if (!SecureToken.IsWellFormed(request.Token))
        {
            return new(AuthOperationStatus.InvalidToken);
        }

        var now = clock.GetUtcNow();
        var hash = SecureToken.Hash(request.Token!);
        var user = await db.Users.SingleOrDefaultAsync(u => u.InviteTokenHash == hash, ct);
        if (user is null || !user.IsActive || user.InviteAcceptedAt is not null || !(user.InviteExpiresAt > now))
        {
            return new(AuthOperationStatus.InvalidToken);
        }

        if (AuthPolicy.ValidatePassword(request.Password) is { } error)
        {
            return new(AuthOperationStatus.InvalidPassword, error);
        }

        user.PasswordHash = hasher.Hash(request.Password!);
        user.InviteAcceptedAt = now;
        // Özet silinince aynı bağlantı ikinci kez kullanılamıyor.
        user.InviteTokenHash = null;
        Audit(user.Id, "user.invite_accept", user.Id, now);
        await db.SaveChangesAsync(ct);
        return AuthOperationResult.Ok;
    }

    /// <summary>
    /// "Şifremi unuttum": hesap varsa ve şifresi belirlenmişse sıfırlama bağlantısı gönderir. Sonuç dışarıya
    /// söylenmiyor; uç her durumda aynı yanıtı veriyor ki e-postanın kayıtlı olup olmadığı anlaşılmasın.
    /// </summary>
    public async Task RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 320)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var normalized = User.NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);

        // Daveti kabul etmemiş kullanıcı davet bağlantısını kullanmalı; pasif kullanıcı giriş yapamaz.
        if (user is null || !user.IsActive || user.PasswordHash is null)
        {
            return;
        }

        // Bekleme süresi dolmadan yeni bağlantı üretilmiyor; önceki bağlantı geçerli kalıyor.
        var lastRequestedAt = user.PasswordResetExpiresAt - AuthPolicy.PasswordResetLifetime;
        if (lastRequestedAt + AuthPolicy.PasswordResetCooldown > now)
        {
            return;
        }

        var token = SecureToken.Generate();
        user.PasswordResetTokenHash = SecureToken.Hash(token);
        user.PasswordResetExpiresAt = now + AuthPolicy.PasswordResetLifetime;
        Audit(user.Id, "user.password_reset_request", user.Id, now);
        await db.SaveChangesAsync(ct);

        await email.SendPasswordResetAsync(user.Email, user.FullName, token, user.PasswordResetExpiresAt.Value, ct);
    }

    /// <summary>
    /// Sıfırlama bağlantısındaki belirteçle yeni şifre belirler. Bağlantı tek kullanımlık; hesap kilidi
    /// kalkıyor ve bütün yenileme belirteçleri iptal ediliyor (şifre değiştirmeyle aynı neden).
    /// </summary>
    public async Task<AuthOperationResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        if (!SecureToken.IsWellFormed(request.Token))
        {
            return new(AuthOperationStatus.InvalidToken);
        }

        var now = clock.GetUtcNow();
        var hash = SecureToken.Hash(request.Token!);
        var user = await db.Users.SingleOrDefaultAsync(u => u.PasswordResetTokenHash == hash, ct);
        if (user is null || !user.IsActive || !(user.PasswordResetExpiresAt > now))
        {
            return new(AuthOperationStatus.InvalidToken);
        }

        if (AuthPolicy.ValidatePassword(request.Password) is { } error)
        {
            return new(AuthOperationStatus.InvalidPassword, error);
        }

        user.PasswordHash = hasher.Hash(request.Password!);
        // Özet silinince aynı bağlantı ikinci kez kullanılamıyor.
        user.PasswordResetTokenHash = null;
        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        await RevokeAllAsync(user.Id, now, ct);
        Audit(user.Id, "user.password_reset", user.Id, now);
        await db.SaveChangesAsync(ct);
        return AuthOperationResult.Ok;
    }

    /// <summary>
    /// Giriş yapmış kullanıcının şifresini değiştirir (GAB 3.4.5 "Profil → Şifre Değiştir"; kartta uç yok).
    /// Bütün yenileme belirteçleri iptal ediliyor: çalınmış bir oturum varsa şifre değişince kapanmalı.
    /// </summary>
    public async Task<AuthOperationResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
        if (user?.PasswordHash is null || string.IsNullOrEmpty(request.CurrentPassword)
            || !hasher.Verify(user.PasswordHash, request.CurrentPassword))
        {
            return new(AuthOperationStatus.WrongCurrentPassword);
        }

        if (AuthPolicy.ValidatePassword(request.NewPassword) is { } error)
        {
            return new(AuthOperationStatus.InvalidPassword, error);
        }

        var now = clock.GetUtcNow();
        user.PasswordHash = hasher.Hash(request.NewPassword!);
        await RevokeAllAsync(user.Id, now, ct);
        Audit(user.Id, "user.password_change", user.Id, now);
        await db.SaveChangesAsync(ct);
        return AuthOperationResult.Ok;
    }

    /// <summary>
    /// JWT her istekte doğrulandıktan sonra çağrılıyor (FR-02): pasife alınan ya da rolü değişen
    /// kullanıcının elindeki belirteç, 15 dakikası dolmadan bir sonraki istekte reddediliyor.
    /// </summary>
    public Task<bool> IsTokenUserValidAsync(Guid userId, UserRole role, CancellationToken ct = default) =>
        db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive && u.Role == role, ct);

    private TokenResponse IssueTokens(User user, DateTimeOffset now) => IssueTokens(user, now, out _);

    private TokenResponse IssueTokens(User user, DateTimeOffset now, out RefreshToken refresh)
    {
        var access = tokens.CreateAccessToken(user);
        var raw = SecureToken.Generate();
        refresh = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = SecureToken.Hash(raw),
            CreatedAt = now,
            ExpiresAt = now + AuthPolicy.RefreshTokenLifetime,
        };
        // Açıkça Add: Id baştan dolu olduğu için navigasyonla bağlanırsa EF UPDATE çalıştırırdı.
        db.RefreshTokens.Add(refresh);
        return new TokenResponse(access.Token, access.ExpiresAt, raw);
    }

    private async Task RevokeAllAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var active = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var token in active)
        {
            token.RevokedAt = now;
        }
    }

    private void Audit(Guid? actorId, string action, Guid targetId, DateTimeOffset now) =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = action,
            TargetType = nameof(User),
            TargetId = targetId,
            OccurredAt = now,
        });
}
