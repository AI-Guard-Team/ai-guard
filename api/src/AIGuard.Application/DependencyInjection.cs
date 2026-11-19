using AIGuard.Application.Events;
using AIGuard.Application.Risk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AIGuard.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Süre hesapları DateTimeOffset.UtcNow yerine TimeProvider ile yapılıyor;
        // testlerde saati ileri alabilmek için (K-15).
        services.AddSingleton(TimeProvider.System);

        // Risk ayarları Api'de riskconfig.json'dan bağlanıyor; burada doğrulanıyor.
        services.AddSingleton<IValidateOptions<RiskOptions>, RiskOptionsValidator>();
        services.AddSingleton(sp => new RiskEngine(sp.GetRequiredService<IOptions<RiskOptions>>().Value));

        services.AddScoped<EventService>();
        services.AddScoped<SessionIpGuard>();

        return services;
    }
}
