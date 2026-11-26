using AIGuard.Application.Audit;
using AIGuard.Infrastructure.Security;
using Microsoft.AspNetCore.Routing;

namespace AIGuard.Api.Audit;

/// <summary>
/// Yanıt 401 ya da 403 ise reddi denetim kaydına yazdırır (GAB 3.3.2, S-27). Yetkilendirmeyi sardığı için hem rol
/// kuralının (<c>[Authorize(Roles=...)]</c>) hem de denetleyicinin döndürdüğü reddi görüyor. Kayıt yazılamazsa istek
/// etkilenmiyor; yalnızca günlüğe yazılıyor.
/// </summary>
public sealed class AccessDeniedAuditMiddleware(RequestDelegate next, ILogger<AccessDeniedAuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, AccessDeniedRecorder recorder)
    {
        await next(context);

        var status = context.Response.StatusCode;
        if (status is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden))
        {
            return;
        }

        // Kullanıcı yalnızca belirteci doğrulanmışsa yazılıyor; 401'de doğrulanmamış bir belirtecin içeriğine güvenilmiyor.
        Guid? userId = context.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(context.User.FindFirst(JwtClaimNames.Subject)?.Value, out var id) ? id : null;
        var path = context.Request.Path.Value ?? string.Empty;
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? path;
        try
        {
            // İstek iptal edilse de kayıt yazılsın: istemcinin bağlantıyı kesmesi reddi kayıttan düşürmemeli.
            await recorder.RecordAsync(userId, status, context.Request.Method, route, path, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Reddedilen istek denetim kaydına yazılamadı: {Status} {Method} {Path}", status, context.Request.Method, path);
        }
    }
}
