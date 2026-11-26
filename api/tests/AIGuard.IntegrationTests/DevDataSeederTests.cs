using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Persistence.Seed;
using AIGuard.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIGuard.IntegrationTests;

/// <summary>
/// Örnek veri testleri. Her test kendi boş veritabanını kullanır (seed "kullanıcı varsa
/// atla" mantığıyla çalıştığı için diğer testlerin verisiyle karışmamalı).
/// </summary>
public class DevDataSeederTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx = new();

    public Task InitializeAsync() => _fx.InitializeAsync();

    public Task DisposeAsync() => _fx.DisposeAsync();

    private DevDataSeeder Seeder(Infrastructure.Persistence.AppDbContext db, string environment, string? password) =>
        new(db,
            new IdentityPasswordHasher(),
            new FakeEnvironment(environment),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DefaultPassword"] = password }).Build(),
            NullLogger<DevDataSeeder>.Instance);

    [SkippableFact]
    public async Task Seeds_expected_data_in_development_and_is_idempotent()
    {
        Skip.If(_fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = _fx.CreateContext();

        await Seeder(db, Environments.Development, "Gelistirme-Sifresi-123").SeedAsync();
        await Seeder(db, Environments.Development, "Gelistirme-Sifresi-123").SeedAsync(); // ikinci çalıştırma

        Assert.Equal(1, await db.Users.CountAsync(u => u.Role == UserRole.Admin));
        Assert.Equal(1, await db.Users.CountAsync(u => u.Role == UserRole.Reviewer));
        Assert.Equal(2 + DevDataSeeder.ExtraCandidates, await db.Users.CountAsync(u => u.Role == UserRole.Candidate));
        Assert.Equal(1, await db.Exams.CountAsync());
        Assert.Equal(3 + DevDataSeeder.ExtraCandidates, await db.ExamAssignments.CountAsync());
        Assert.True(await db.Questions.CountAsync() >= 1);
    }

    [SkippableFact]
    public async Task Seeded_password_is_hashed_not_plain()
    {
        Skip.If(_fx.ConnectionString is null, "PostgreSQL yok.");
        const string password = "Gelistirme-Sifresi-123";
        await using var db = _fx.CreateContext();

        await Seeder(db, Environments.Development, password).SeedAsync();

        var admin = await db.Users.SingleAsync(u => u.Role == UserRole.Admin);
        Assert.NotNull(admin.PasswordHash);
        Assert.DoesNotContain(password, admin.PasswordHash);
        Assert.True(new IdentityPasswordHasher().Verify(admin.PasswordHash!, password));
    }

    [SkippableFact]
    public async Task Without_password_users_cannot_log_in()
    {
        Skip.If(_fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = _fx.CreateContext();

        await Seeder(db, Environments.Development, password: null).SeedAsync();

        Assert.True(await db.Users.AllAsync(u => u.PasswordHash == null));
    }

    [SkippableFact]
    public async Task Does_nothing_outside_development()
    {
        Skip.If(_fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = _fx.CreateContext();

        await Seeder(db, Environments.Production, "Gelistirme-Sifresi-123").SeedAsync();

        Assert.False(await db.Users.AnyAsync());
    }

    [SkippableFact]
    public async Task Rejects_short_seed_password()
    {
        Skip.If(_fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = _fx.CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Seeder(db, Environments.Development, "123").SeedAsync());
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "AIGuard.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
