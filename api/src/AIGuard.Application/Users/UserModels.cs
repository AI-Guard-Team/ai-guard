using AIGuard.Domain.Enums;

namespace AIGuard.Application.Users;

public sealed record CreateUserRequest(string? FullName, string? Email, UserRole? Role);

/// <summary>Yalnızca gönderilen alanlar değişiyor (FR-03: rol atama, pasife alma).</summary>
public sealed record UpdateUserRequest(UserRole? Role, bool? IsActive);

public enum InviteStatus
{
    /// <summary>Davet gönderildi, şifre henüz belirlenmedi.</summary>
    Pending,

    /// <summary>Davetin süresi doldu; şifre belirlenmedi.</summary>
    Expired,

    Accepted,
}

/// <summary>Yönetici kullanıcı listesindeki satır. Şifre özeti ve belirteçler hiçbir zaman dönmüyor.</summary>
public sealed record UserSummary(
    Guid Id,
    string FullName,
    string Email,
    UserRole Role,
    bool IsActive,
    InviteStatus InviteStatus,
    DateTimeOffset CreatedAt);

public enum UserOperationStatus
{
    Success,
    NotFound,
    Invalid,

    /// <summary>Bu e-postayla bir hesap zaten var.</summary>
    EmailTaken,
}

public sealed record UserOperationResult(UserOperationStatus Status, string? Error = null, Guid? Id = null);
