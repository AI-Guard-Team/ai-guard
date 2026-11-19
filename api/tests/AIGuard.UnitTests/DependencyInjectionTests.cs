using AIGuard.Application;
using AIGuard.Application.Abstractions;
using AIGuard.Infrastructure;
using AIGuard.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIGuard.UnitTests;

/// <summary>Servis kayıtlarının doğru bağlandığını kontrol eder (veritabanına bağlanmaz).</summary>
public class DependencyInjectionTests
{
    private static ServiceProvider BuildProvider(string? connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = connectionString })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public void Abstractions_resolve_to_infrastructure_implementations()
    {
        using var provider = BuildProvider("Host=127.0.0.1;Port=1;Database=none;Username=none");
        using var scope = provider.CreateScope();

        var appDb = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        // Aynı scope'ta arayüz ve AppDbContext aynı örnek olmalı, yoksa iki ayrı change tracker oluşur.
        Assert.Same(scope.ServiceProvider.GetRequiredService<AppDbContext>(), appDb);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_connection_string_fails_fast(string? connectionString)
    {
        // Bağlantı ayarı yoksa uygulama açılışta durmalı.
        var ex = Assert.Throws<InvalidOperationException>(() => BuildProvider(connectionString));
        Assert.Contains("ConnectionStrings", ex.Message);
    }
}
