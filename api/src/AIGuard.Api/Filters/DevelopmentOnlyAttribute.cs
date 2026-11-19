using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AIGuard.Api.Filters;

/// <summary>
/// Uç yalnızca Development ortamında çalışıyor, diğer ortamlarda 404 dönüyor.
/// </summary>
/// <remarks>
/// Kimlik doğrulama (E2) henüz yok. Olay ucu "bu oturum bu adaya mı ait" kontrolü yapmadan
/// dışarıya açılırsa herkes herhangi bir oturuma olay yazabilir. E2 birleşince bu öznitelik
/// kaldırılıp yerine aday rolü ve oturum sahipliği kontrolü eklenecek.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DevelopmentOnlyAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var env = context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        if (!env.IsDevelopment())
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
