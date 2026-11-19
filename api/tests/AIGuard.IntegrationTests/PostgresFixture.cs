using AIGuard.Infrastructure;
using AIGuard.Infrastructure.Persistence;
using AIGuard.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AIGuard.IntegrationTests;

/// <summary>
/// Her test sınıfı için boş bir veritabanı açıp göçleri uygular, sonunda siler.
/// </summary>
/// <remarks>
/// ConnectionStrings__Default tanımlı değilse testler atlanır (K-09).
/// Bağlanılan kullanıcının CREATEDB yetkisi olmalı.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private string? _adminConnectionString;
    private string? _databaseName;

    /// <summary>Geçici test veritabanının bağlantı dizesi; PostgreSQL yoksa null.</summary>
    public string? ConnectionString { get; private set; }

    public async Task InitializeAsync()
    {
        _adminConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(_adminConnectionString))
        {
            return;
        }

        // Ad yalnızca sabit önek ve Guid'den oluştuğu için SQL'e doğrudan yazılabilir.
        _databaseName = $"aiguard_it_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(_adminConnectionString))
        {
            await admin.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
            await cmd.ExecuteNonQueryAsync();
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = _databaseName,
        }.ConnectionString;

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public AppDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        DependencyInjection.ConfigureDbContext(builder, ConnectionString!)
            .AddInterceptors(new AppendOnlyAuditLogInterceptor());
        return new AppDbContext(builder.Options);
    }

    public async Task DisposeAsync()
    {
        if (_databaseName is null)
        {
            return;
        }

        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", admin);
        await cmd.ExecuteNonQueryAsync();
    }
}
