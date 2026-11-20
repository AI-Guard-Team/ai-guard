using AIGuard.Application.Abstractions;
using AIGuard.Infrastructure.Email;
using AIGuard.Infrastructure.Persistence;
using AIGuard.Infrastructure.Persistence.Interceptors;
using AIGuard.Infrastructure.Persistence.Seed;
using AIGuard.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIGuard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Koda gömülü varsayılan bağlantı yok; ayar eksikse uygulama açılışta duruyor.
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default tanımlı değil. ConnectionStrings__Default ortam değişkeni " +
                "veya dotnet user-secrets ile ayarlayın (bkz. README).");
        }

        services.AddSingleton<AppendOnlyAuditLogInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) =>
            ConfigureDbContext(options, connectionString)
                .AddInterceptors(sp.GetRequiredService<AppendOnlyAuditLogInterceptor>()));

        // Servisler arayüzü istediğinde aynı scope'taki AppDbContext örneği dönüyor.
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

        // E2: JWT anahtarı eksik ya da kısaysa uygulama açılışta duruyor (bağlantı dizesiyle aynı davranış).
        services.AddSingleton(JwtSettings.FromConfiguration(configuration));
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IEmailSender, LoggingEmailSender>();
        services.AddScoped<DevDataSeeder>();

        return services;
    }

    // Uygulama, testler ve dotnet ef aynı ayarları kullansın diye tek yerde.
    public static DbContextOptionsBuilder ConfigureDbContext(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention();
}
