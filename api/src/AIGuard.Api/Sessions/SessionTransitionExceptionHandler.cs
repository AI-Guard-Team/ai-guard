using AIGuard.Domain.Sessions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Sessions;

/// <summary>
/// Tabloda olmayan durum geçişi 409 olarak dönüyor (kart E4). Servisler bilinen durumları önceden
/// kontrol ediyor; buraya düşen, beklenmeyen bir yoldan denenen geçiş.
/// </summary>
public sealed class SessionTransitionExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not InvalidSessionTransitionException transition)
        {
            return false;
        }

        context.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Geçersiz durum geçişi.",
                Detail = $"Oturum {transition.From} durumundayken bu işlem yapılamaz.",
            },
        });
    }
}
