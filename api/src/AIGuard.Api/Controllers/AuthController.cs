using AIGuard.Api.Auth;
using AIGuard.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

/// <summary>Giriş, belirteç yenileme, çıkış ve şifre işlemleri (E2, FR-01, FR-02).</summary>
/// <remarks>Açık kayıt (self-register) ucu yok: hesapları yalnızca yönetici oluşturuyor (UsersController).</remarks>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    // Hata ayrıntıları ProblemDetails'in "detail" alanında; önyüz bu metni kullanıcıya gösteriyor.
    private const string InvalidCredentials = "E-posta veya şifre hatalı.";

    [HttpPost("login")]
    [AllowAnonymous]
    [RequestSizeLimit(2048)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        NoStore();
        return result.Status switch
        {
            LoginStatus.Success => Ok(result.Tokens),
            LoginStatus.LockedOut => Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Hesap geçici olarak kilitlendi.",
                detail: $"Çok fazla hatalı giriş denemesi. {MinutesLeft(result.LockoutEndAt)} dakika sonra tekrar deneyin."),
            // E-postanın var olup olmadığı, hesabın pasif olup olmadığı ayrılmıyor.
            _ => Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Giriş başarısız.", detail: InvalidCredentials),
        };
    }

    [HttpPost("refresh")]
    [AllowAnonymous] // erişim belirtecinin süresi dolmuşken çağrılıyor; kanıt yenileme belirtecinin kendisi
    [RequestSizeLimit(1024)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var result = await auth.RefreshAsync(request, ct);
        NoStore();
        return result.Status == AuthOperationStatus.Success
            ? Ok(result.Tokens)
            : Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Oturum süresi doldu.",
                detail: "Oturumunuzun süresi doldu. Lütfen tekrar giriş yapın.");
    }

    [HttpPost("logout")]
    [AllowAnonymous] // önyüz yenileme belirtecini gönderiyor; erişim belirteci süresi dolmuş olabilir
    [RequestSizeLimit(1024)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request, ct);
        return NoContent();
    }

    [HttpPost("set-password")]
    [AllowAnonymous] // kanıt davet bağlantısındaki tek kullanımlık belirteç
    [RequestSizeLimit(2048)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetPassword(SetPasswordRequest request, CancellationToken ct)
    {
        var result = await auth.SetPasswordAsync(request, ct);
        return result.Status switch
        {
            AuthOperationStatus.Success => NoContent(),
            AuthOperationStatus.InvalidPassword => Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Şifre kurala uymuyor.", detail: result.Error),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Geçersiz davet.",
                detail: "Davet bağlantısı geçersiz, kullanılmış veya süresi dolmuş. Yöneticinizden yeni davet isteyin."),
        };
    }

    /// <summary>Kartta yok; önyüzdeki profil sayfasının "şifre değiştir" formu için (GAB 3.4.5, K-20).</summary>
    [HttpPost("change-password")]
    [Authorize]
    [RequestSizeLimit(2048)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var result = await auth.ChangePasswordAsync(User.GetUserId(), request, ct);
        return result.Status switch
        {
            AuthOperationStatus.Success => NoContent(),
            AuthOperationStatus.InvalidPassword => Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Şifre kurala uymuyor.", detail: result.Error),
            // 401 değil 400: 401 önyüzde belirteç yenilemeyi tetikleyip kullanıcıyı çıkarırdı.
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Mevcut şifre hatalı.",
                detail: "Mevcut şifreniz hatalı."),
        };
    }

    // Belirteç içeren yanıtlar tarayıcı veya ara sunucu önbelleğine yazılmasın.
    private void NoStore() => Response.Headers.CacheControl = "no-store";

    private static int MinutesLeft(DateTimeOffset? until) =>
        until is null ? (int)AuthPolicy.LockoutDuration.TotalMinutes
            : Math.Max(1, (int)Math.Ceiling((until.Value - DateTimeOffset.UtcNow).TotalMinutes));
}
