using AIGuard.Infrastructure;
using AIGuard.Infrastructure.Persistence;
using AIGuard.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.UnitTests;

/// <summary>
/// Veritabanına bağlanmayan bir bağlam üretir. Model bağlantı açılmadan kurulabildiği
/// için birim testleri PostgreSQL gerektirmiyor.
/// </summary>
internal static class TestDbContextFactory
{
    // Erişilemeyen bir adres; bağlanmaya çalışan test hemen hata verir.
    private const string UnreachableConnectionString = "Host=127.0.0.1;Port=1;Database=none;Username=none;Timeout=1";

    public static AppDbContext Create()
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        DependencyInjection.ConfigureDbContext(builder, UnreachableConnectionString)
            .AddInterceptors(new AppendOnlyAuditLogInterceptor());
        return new AppDbContext(builder.Options);
    }
}
