using Microsoft.AspNetCore.Identity;
using AppPasswordHasher = AIGuard.Application.Abstractions.IPasswordHasher;

namespace AIGuard.Infrastructure.Security;

/// <summary>ASP.NET Core Identity'nin PasswordHasher'ı (PBKDF2, tuzlu).</summary>
public sealed class IdentityPasswordHasher : AppPasswordHasher
{
    // Varsayılan PasswordHasher kullanıcı nesnesini kullanmıyor.
    private static readonly object NoUser = new();
    private readonly PasswordHasher<object> _inner = new();

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return _inner.HashPassword(NoUser, password);
    }

    public bool Verify(string passwordHash, string password)
    {
        if (string.IsNullOrEmpty(passwordHash) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        try
        {
            // SuccessRehashNeeded da doğru şifre anlamına geliyor.
            return _inner.VerifyHashedPassword(NoUser, passwordHash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // Bozuk özet girişte 500 hatasına dönüşmesin.
            return false;
        }
    }
}
