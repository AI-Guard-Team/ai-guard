using AIGuard.Application.Auth;
using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace AIGuard.Api.Auth;

/// <summary>JWT doğrulaması ve yetkilendirme varsayılanları (E2, K-20).</summary>
public static class AuthenticationSetup
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Ayarlar JwtSettings'ten (AddInfrastructure'da kayıtlı); belirteci üreten tarafla aynı parametreler.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtSettings>((options, settings) =>
            {
                options.TokenValidationParameters = settings.ValidationParameters();

                // "sub", "role" gibi kısa adlar uzun URI'lere çevrilmesin; RoleClaimType "role".
                options.MapInboundClaims = false;

                options.Events = new JwtBearerEvents
                {
                    // SignalR (A4): tarayıcı WebSocket bağlantısında başlık gönderemiyor, belirteci
                    // access_token sorgu parametresiyle yolluyor. Yalnızca /hubs altında kabul ediliyor;
                    // REST uçlarında belirteç URL'de (ve sunucu günlüklerinde) taşınmasın.
                    OnMessageReceived = context =>
                    {
                        if (context.HttpContext.Request.Path.StartsWithSegments("/hubs")
                            && context.Request.Query["access_token"] is { Count: 1 } token)
                        {
                            context.Token = token.ToString();
                        }

                        return Task.CompletedTask;
                    },

                    // FR-02: imza ve süre geçerli olsa da kullanıcı pasife alınmış ya da rolü
                    // değişmişse belirteç bir sonraki istekte reddediliyor.
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal!;
                        if (!Guid.TryParse(principal.FindFirst(JwtClaimNames.Subject)?.Value, out var userId)
                            || !Enum.TryParse<UserRole>(principal.FindFirst(JwtClaimNames.Role)?.Value, out var role))
                        {
                            context.Fail("Belirteçte kullanıcı bilgisi eksik.");
                            return;
                        }

                        var auth = context.HttpContext.RequestServices.GetRequiredService<AuthService>();
                        if (!await auth.IsTokenUserValidAsync(userId, role, context.HttpContext.RequestAborted))
                        {
                            context.Fail("Kullanıcı pasif veya rolü değişmiş.");
                        }
                    },
                };
            });

        services.AddAuthorization(options =>
        {
            // Varsayılan olarak her uç giriş istiyor. Herkese açık uçlar [AllowAnonymous] ile
            // açıkça işaretleniyor; yetki koymayı unutan yeni bir uç kendiliğinden kapalı kalıyor.
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

            // Erişim matrisi (GAB 3.3.8, K-32): uçlar rol adı yerine bu policy'leri kullanıyor.
            AccessPolicies.Add(options);
        });

        return services;
    }
}
