using System.Reflection;
using AIGuard.Application.Abstractions;
using AIGuard.Domain.Common;
using AIGuard.Infrastructure.Persistence;

namespace AIGuard.UnitTests;

/// <summary>
/// Katman bağımlılıklarını kontrol eder (K-14). Yanlış yöne eklenen bir referans
/// (ör. Domain'e EF Core) derlemeyi bozmadığı için fark edilmesi zor; bu testler yakalıyor.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Entity).Assembly;
    private static readonly Assembly Application = typeof(IAppDbContext).Assembly;
    private static readonly Assembly Infrastructure = typeof(AppDbContext).Assembly;

    private static IEnumerable<string> ReferencesOf(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

    [Fact]
    public void Domain_depends_on_nothing_but_the_base_library()
    {
        // Domain saf C#: EF Core, ASP.NET, Npgsql veya diğer katmanlar yok.
        string[] forbiddenPrefixes = ["AIGuard.", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "Microsoft.Extensions"];

        var violations = ReferencesOf(Domain).Where(r => forbiddenPrefixes.Any(r.StartsWith));

        Assert.Empty(violations);
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_api_or_database_provider()
    {
        string[] forbidden = ["AIGuard.Infrastructure", "AIGuard.Api", "Npgsql", "Npgsql.EntityFrameworkCore.PostgreSQL", "Microsoft.AspNetCore"];

        var violations = ReferencesOf(Application).Where(r => forbidden.Any(f => r == f || r.StartsWith(f + ".")));

        Assert.Empty(violations);
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        Assert.DoesNotContain("AIGuard.Api", ReferencesOf(Infrastructure));
    }

    [Fact]
    public void All_entities_live_in_domain_and_derive_from_Entity()
    {
        // Varlık eklerken Domain/Entities altına koyun ve Entity'den türetin (Guid Id, K-02).
        using var db = TestDbContextFactory.Create();

        Assert.All(db.Model.GetEntityTypes(), e =>
        {
            Assert.Equal(Domain, e.ClrType.Assembly);
            Assert.True(typeof(Entity).IsAssignableFrom(e.ClrType), $"{e.ClrType.Name} Entity'den türemeli.");
        });
    }

    [Fact]
    public void IAppDbContext_exposes_every_entity_set()
    {
        // Yeni varlık AppDbContext'e eklenip arayüze eklenmeyi unutulursa Application ona erişemez.
        using var db = TestDbContextFactory.Create();
        var exposed = typeof(IAppDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType)
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .ToHashSet();

        Assert.All(db.Model.GetEntityTypes(), e => Assert.Contains(e.ClrType, exposed));
    }
}
