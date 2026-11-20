using Microsoft.OpenApi.Models;

namespace AIGuard.Api;

/// <summary>
/// Swagger (OpenAPI) dokümanı (E3). Yalnızca Development'ta açık: uç listesini herkese açık
/// sunucuda yayımlamak saldırı yüzeyini tanıtmak olur.
/// </summary>
public static class SwaggerSetup
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo { Title = "AI-Guard API", Version = "v1" });

            // Controller'lardaki /// açıklamaları Swagger'da görünsün.
            var xml = Path.Combine(AppContext.BaseDirectory, "AIGuard.Api.xml");
            if (File.Exists(xml))
            {
                o.IncludeXmlComments(xml);
            }

            // "Authorize" düğmesi: /api/auth/login'den alınan accessToken yapıştırılıp uçlar denenebiliyor.
            var bearer = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "POST /api/auth/login yanıtındaki accessToken",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            };
            o.AddSecurityDefinition("Bearer", bearer);
            o.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = [] });
        });
        return services;
    }

    public static WebApplication UseApiDocumentation(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(o => o.DocumentTitle = "AI-Guard API");
        }

        return app;
    }
}
