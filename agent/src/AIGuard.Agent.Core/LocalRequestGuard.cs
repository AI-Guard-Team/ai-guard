using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AIGuard.Agent.Core;

/// <summary>
/// Yerel uca gelen her isteği süzer: Host başlığı, origin ve CORS ön kontrolü (preflight).
/// </summary>
/// <remarks>
/// CORS başlıkları yalnızca tarayıcının yanıtı okumasını engeller; isteğin çalışmasını engellemez.
/// Bu yüzden izinsiz origin'den gelen istek uca hiç ulaşmadan 403 ile reddediliyor.
/// </remarks>
internal sealed class LocalRequestGuard
{
    private readonly RequestDelegate _next;
    private readonly HashSet<string> _allowedOrigins;

    public LocalRequestGuard(RequestDelegate next, IOptions<AgentOptions> options)
    {
        _next = next;
        _allowedOrigins = options.Value.AllowedOrigins
            .Select(NormalizeOrigin)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // DNS rebinding: kötü niyetli bir site kendi alan adını 127.0.0.1'e çözdürüp ajana
        // "aynı origin" gibi istek atabilir. O durumda Host başlığı o alan adı olur; reddediyoruz.
        if (!IsLoopbackHost(context.Request.Host))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // Tarayıcı başka bir siteden gelen fetch isteğinde Origin'i her zaman ekler ve sayfa bunu
        // değiştiremez. Origin'siz istek tarayıcıdan gelmiyor demektir; eşleştirme yalnızca tarayıcıyla.
        var origin = context.Request.Headers.Origin.ToString();
        if (NormalizeOrigin(origin) is not { } normalized || !_allowedOrigins.Contains(normalized))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var headers = context.Response.Headers;
        headers.AccessControlAllowOrigin = origin;
        // Yanıt origin'e göre değiştiği için ara bellekler farklı origin'lere aynı yanıtı vermesin.
        headers.Vary = "Origin";

        if (HttpMethods.IsOptions(context.Request.Method))
        {
            // Ön kontrol: JSON gövdeli POST'tan önce tarayıcı izin soruyor.
            headers.AccessControlAllowMethods = "POST";
            headers.AccessControlAllowHeaders = "Content-Type";
            headers.AccessControlMaxAge = "600";

            // Chrome'un "Private Network Access" kuralı: genel bir siteden 127.0.0.1'e istek için
            // sunucunun bu başlıkla açıkça izin vermesi gerekiyor (kart: Y1).
            headers["Access-Control-Allow-Private-Network"] = "true";

            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        await _next(context);
    }

    /// <summary>"https://Ornek.edu.tr:443/" → "https://ornek.edu.tr". Geçersizse null.</summary>
    internal static string? NormalizeOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)
            || !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0
            || !string.IsNullOrEmpty(uri.UserInfo) || origin.Contains('*'))
        {
            return null;
        }

        // Varsayılan port (443/80) tarayıcının Origin başlığında yazılmıyor; karşılaştırma için çıkarılıyor.
        return uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
    }

    private static bool IsLoopbackHost(HostString host) =>
        host.Host is "127.0.0.1" or "localhost";
}
