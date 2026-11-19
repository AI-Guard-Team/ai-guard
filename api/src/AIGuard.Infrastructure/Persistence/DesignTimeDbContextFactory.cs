using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AIGuard.Infrastructure.Persistence;

/// <summary>
/// Yalnızca dotnet ef komutları için. "migrations add" veritabanına bağlanmadığından
/// bağlantı ayarı olmadan da göç eklenebiliyor; "database update" ise user-secrets veya
/// ortam değişkenindeki gerçek bağlantıyı kullanıyor.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    // AIGuard.Api.csproj'daki UserSecretsId ile aynı olmalı.
    private const string ApiUserSecretsId = "aiguard-api-dev";

    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(ApiUserSecretsId)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=aiguard;Username=aiguard";

        var options = DependencyInjection.ConfigureDbContext(
            new DbContextOptionsBuilder<AppDbContext>(), connectionString);

        return new AppDbContext((DbContextOptions<AppDbContext>)options.Options);
    }
}
