using System.Globalization;
using Microsoft.Net.Http.Headers;

namespace AIGuard.Api.Localization;

/// <summary>
/// <c>Accept-Language</c> başlığından arayüz dilini seçer (K-48): en yüksek öncelikli "en" ya da "tr".
/// Seçim <see cref="AIGuard.Application.Localization.ApiLanguage"/>'a istek boyunca yazılır; kültür
/// (<see cref="CultureInfo"/>) değişmez, böylece mevcut biçimlendirme ve ayrıştırma etkilenmez. Tanınmayan dil Türkçe sayılır.
/// </summary>
public sealed class UiLanguageMiddleware(RequestDelegate next)
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    public async Task InvokeAsync(HttpContext context)
    {
        AIGuard.Application.Localization.ApiLanguage.Set(Pick(context.Request.Headers.AcceptLanguage.ToString()) == English);
        await next(context);
    }

    public static CultureInfo Pick(string header)
    {
        if (string.IsNullOrWhiteSpace(header) || !StringWithQualityHeaderValue.TryParseList(header.Split(','), out var values))
        {
            return Turkish;
        }

        foreach (var value in values.OrderByDescending(v => v.Quality ?? 1))
        {
            var tag = value.Value.Value ?? "";
            if (tag.StartsWith("tr", StringComparison.OrdinalIgnoreCase)) return Turkish;
            if (tag.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return English;
        }

        return Turkish;
    }
}
