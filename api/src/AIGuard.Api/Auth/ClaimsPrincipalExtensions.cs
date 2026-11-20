using System.Security.Claims;
using AIGuard.Infrastructure.Security;

namespace AIGuard.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Giriş yapmış kullanıcının kimliği. Yalnızca [Authorize] altındaki uçlarda çağrılmalı;
    /// orada belirteç zaten doğrulanmış ve "sub" talebi kontrol edilmiş oluyor.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirst(JwtClaimNames.Subject)?.Value
            ?? throw new InvalidOperationException("Kimliği doğrulanmamış istekte kullanıcı kimliği istendi."));
}
