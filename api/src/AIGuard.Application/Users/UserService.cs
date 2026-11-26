using System.Net.Mail;
using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Auth;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Users;

/// <summary>
/// Yöneticinin kullanıcı oluşturup davet etmesi, listelemesi, rol ataması ve pasife alması (E2, FR-01, FR-03).
/// Açık kayıt yok: hesap yalnızca buradan oluşuyor.
/// </summary>
public sealed class UserService(IAppDbContext db, IEmailSender email, TimeProvider clock)
{
    public async Task<UserOperationResult> CreateAsync(Guid actorId, CreateUserRequest request, CancellationToken ct = default)
    {
        var fullName = request.FullName?.Trim();
        if (string.IsNullOrEmpty(fullName) || fullName.Length > 200)
        {
            return new(UserOperationStatus.Invalid, "Ad soyad 1–200 karakter olmalıdır.");
        }

        // MailAddress "Ad <adres>" biçimini de kabul ediyor; yalnızca çıplak adresi alıyoruz.
        var address = request.Email?.Trim();
        if (string.IsNullOrEmpty(address) || address.Length > 320
            || !MailAddress.TryCreate(address, out var parsed) || parsed.Address != address)
        {
            return new(UserOperationStatus.Invalid, "Geçerli bir e-posta adresi girin.");
        }

        if (request.Role is not { } role || !Enum.IsDefined(role))
        {
            return new(UserOperationStatus.Invalid, "Rol Admin, Reviewer veya Candidate olmalıdır.");
        }

        var normalized = User.NormalizeEmail(address);
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct))
        {
            return new(UserOperationStatus.EmailTaken, "Bu e-posta adresiyle bir hesap zaten var.");
        }

        var now = clock.GetUtcNow();
        var token = SecureToken.Generate();
        var user = new User
        {
            FullName = fullName,
            Email = address,
            NormalizedEmail = normalized,
            Role = role,
            InviteTokenHash = SecureToken.Hash(token),
            InviteExpiresAt = now + AuthPolicy.InviteLifetime,
            InviteSentAt = now,
            CreatedAt = now,
        };
        db.Users.Add(user);
        Audit(actorId, "user.create", user.Id, now, newValue: new { role = role.ToString() });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (db.IsUniqueViolation(ex))
        {
            // Aynı e-postayla eşzamanlı iki istek: kontrolü ikisi de geçti, veritabanı ikincisini reddetti (K-11).
            return new(UserOperationStatus.EmailTaken, "Bu e-posta adresiyle bir hesap zaten var.");
        }

        // Kayıttan sonra: gönderim başarısız olursa hesap yine oluşmuş oluyor; davet yeniden
        // gönderme ucu ileride gerekirse eklenebilir (kartta yok).
        await email.SendInviteAsync(user.Email, user.FullName, token, user.InviteExpiresAt!.Value, ct);
        return new(UserOperationStatus.Success, Id: user.Id);
    }

    public async Task<IReadOnlyList<UserSummary>> ListAsync(UserRole? role, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var query = db.Users.AsNoTracking();
        if (role is { } r)
        {
            query = query.Where(u => u.Role == r);
        }

        var users = await query.OrderBy(u => u.FullName).ToListAsync(ct);
        return users.Select(u => new UserSummary(
            u.Id, u.FullName, u.Email, u.Role, u.IsActive,
            u.InviteAcceptedAt is not null ? InviteStatus.Accepted
                : u.InviteExpiresAt > now ? InviteStatus.Pending : InviteStatus.Expired,
            u.CreatedAt,
            u.LastLoginAt)).ToList();
    }

    public async Task<UserOperationResult> UpdateAsync(Guid actorId, Guid userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        if (request.Role is null && request.IsActive is null)
        {
            return new(UserOperationStatus.Invalid, "Değiştirilecek alan yok.");
        }

        if (request.Role is { } r && !Enum.IsDefined(r))
        {
            return new(UserOperationStatus.Invalid, "Rol Admin, Reviewer veya Candidate olmalıdır.");
        }

        // Yönetici kendi rolünü düşürür ya da kendini pasife alırsa sistemde yönetici kalmayabilir.
        if (userId == actorId)
        {
            return new(UserOperationStatus.Invalid, "Kendi rolünüzü veya durumunuzu değiştiremezsiniz.");
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return new(UserOperationStatus.NotFound);
        }

        var now = clock.GetUtcNow();
        if (request.Role is { } newRole && newRole != user.Role)
        {
            // FR-35: rol değişikliği denetim kaydına. Eski belirteç rol uyuşmadığı için bir sonraki
            // istekte reddediliyor (AuthService.IsTokenUserValidAsync).
            Audit(actorId, "user.role_change", user.Id, now, new { role = user.Role.ToString() }, new { role = newRole.ToString() });
            user.Role = newRole;
        }

        if (request.IsActive is { } active && active != user.IsActive)
        {
            Audit(actorId, active ? "user.activate" : "user.deactivate", user.Id, now,
                new { isActive = user.IsActive }, new { isActive = active });
            user.IsActive = active;

            if (!active)
            {
                // Pasife alınan kullanıcı yenileme belirteciyle de yeni erişim belirteci alamamalı.
                var tokens = await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(ct);
                foreach (var token in tokens)
                {
                    token.RevokedAt = now;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return new(UserOperationStatus.Success, Id: user.Id);
    }

    /// <summary>
    /// Daveti yeniden gönderir (K-49): yeni tek kullanımlık bağlantı, eski bağlantı geçersiz, süre yeniden 72 saat.
    /// Daveti kabul etmiş ya da pasif kullanıcıya gönderilmez; aynı kullanıcıya en fazla dakikada bir.
    /// </summary>
    public async Task<UserOperationResult> ResendInviteAsync(Guid actorId, Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return new(UserOperationStatus.NotFound);
        }

        if (user.InviteAcceptedAt is not null)
        {
            return new(UserOperationStatus.Conflict, "Kullanıcı daveti kabul etmiş; davet yeniden gönderilemez.");
        }

        if (!user.IsActive)
        {
            return new(UserOperationStatus.Conflict, "Pasif kullanıcıya davet gönderilemez.");
        }

        var now = clock.GetUtcNow();
        if (user.InviteSentAt is { } sent && now - sent < AuthPolicy.InviteResendCooldown)
        {
            return new(UserOperationStatus.TooSoon, "Davet az önce gönderildi; birkaç dakika sonra yeniden deneyin.");
        }

        var token = SecureToken.Generate();
        user.InviteTokenHash = SecureToken.Hash(token);
        user.InviteExpiresAt = now + AuthPolicy.InviteLifetime;
        user.InviteSentAt = now;
        Audit(actorId, "user.invite_resend", user.Id, now);
        await db.SaveChangesAsync(ct);

        await email.SendInviteAsync(user.Email, user.FullName, token, user.InviteExpiresAt.Value, ct);
        return new(UserOperationStatus.Success, Id: user.Id);
    }

    private void Audit(Guid actorId, string action, Guid targetId, DateTimeOffset now, object? oldValue = null, object? newValue = null) =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = action,
            TargetType = nameof(User),
            TargetId = targetId,
            // jsonb sütunları; yalnızca rol ve durum yazılıyor, gizli değer yok (K-10).
            OldValue = oldValue is null ? null : JsonSerializer.Serialize(oldValue),
            NewValue = newValue is null ? null : JsonSerializer.Serialize(newValue),
            OccurredAt = now,
        });
}
