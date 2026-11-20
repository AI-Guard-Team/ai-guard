namespace AIGuard.Application.Auth;

// Gövde alanları önyüzün gönderdiğiyle aynı (web/src/api/auth.ts, client.ts).
// Alanlar null olabilir: eksik alan model doğrulamasında değil serviste Türkçe mesajla reddediliyor.

public sealed record LoginRequest(string? Email, string? Password);

public sealed record RefreshRequest(string? RefreshToken);

public sealed record SetPasswordRequest(string? Token, string? Password);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

/// <summary>Giriş ve yenilemenin yanıtı. Önyüz accessToken ve refreshToken alanlarını okuyor.</summary>
public sealed record TokenResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken);

public enum LoginStatus
{
    Success,

    /// <summary>E-posta yok, şifre yanlış, hesap pasif ya da davet henüz kabul edilmemiş; nedeni dışarıya söylenmiyor.</summary>
    InvalidCredentials,

    LockedOut,
}

public sealed record LoginResult(LoginStatus Status, TokenResponse? Tokens = null, DateTimeOffset? LockoutEndAt = null);

public enum AuthOperationStatus
{
    Success,

    /// <summary>Belirteç yok, süresi dolmuş, kullanılmış ya da kullanıcı pasif.</summary>
    InvalidToken,

    /// <summary>Şifre kurala uymuyor; mesaj <see cref="AuthOperationResult.Error"/>'da.</summary>
    InvalidPassword,

    /// <summary>Şifre değiştirirken mevcut şifre yanlış.</summary>
    WrongCurrentPassword,
}

public sealed record AuthOperationResult(AuthOperationStatus Status, string? Error = null, TokenResponse? Tokens = null)
{
    public static readonly AuthOperationResult Ok = new(AuthOperationStatus.Success);
}
