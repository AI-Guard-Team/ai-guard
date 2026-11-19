using Microsoft.Extensions.DependencyInjection;

namespace AIGuard.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Süre hesapları DateTimeOffset.UtcNow yerine TimeProvider ile yapılıyor;
        // testlerde saati ileri alabilmek için (K-15).
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
