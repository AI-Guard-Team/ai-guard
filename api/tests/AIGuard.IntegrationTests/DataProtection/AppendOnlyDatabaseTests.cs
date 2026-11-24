using AIGuard.Application.ConsentTexts;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AIGuard.IntegrationTests.DataProtection;

/// <summary>
/// A5: denetim kaydı ve rıza metinleri veritabanında da yalnızca eklemeli (K-10 ikinci adım, K-32). Interceptor'a
/// uğramayan yollar (ham SQL, ExecuteUpdate, TRUNCATE) da reddedilmeli.
/// </summary>
public class AppendOnlyDatabaseTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    [SkippableFact]
    public async Task Raw_sql_cannot_update_delete_or_truncate_the_audit_log()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var log = new AuditLog { Action = "test.append_only", NewValue = """{"a":1}""" };
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();

        await AssertRejectedAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE audit_logs SET action = 'x' WHERE id = {log.Id}"));
        await AssertRejectedAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM audit_logs WHERE id = {log.Id}"));
        await AssertRejectedAsync(() => db.Database.ExecuteSqlRawAsync("TRUNCATE audit_logs"));

        db.ChangeTracker.Clear();
        var after = await db.AuditLogs.SingleAsync(a => a.Id == log.Id);
        Assert.Equal("test.append_only", after.Action);
    }

    [SkippableFact]
    public async Task ExecuteUpdate_and_ExecuteDelete_bypass_the_interceptor_but_not_the_trigger()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var log = new AuditLog { Action = "test.bypass" };
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();

        await AssertRejectedAsync(() => db.AuditLogs.Where(a => a.Id == log.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(a => a.Action, "x")));
        await AssertRejectedAsync(() => db.AuditLogs.Where(a => a.Id == log.Id).ExecuteDeleteAsync());

        // Saklama görevinin yolu da 1 yıldan yeni kaydı silemiyor.
        await AssertRejectedAsync(() => db.DeleteAuditLogsOlderThanAsync(DateTimeOffset.UtcNow.AddDays(1)));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Id == log.Id));
    }

    [SkippableFact]
    public async Task Only_audit_logs_older_than_a_year_can_be_deleted()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var old = new AuditLog { Action = "test.expired", OccurredAt = DateTimeOffset.UtcNow.AddDays(-400) };
        db.AuditLogs.Add(old);
        await db.SaveChangesAsync();

        var deleted = await db.DeleteAuditLogsOlderThanAsync(DateTimeOffset.UtcNow.AddDays(-366));

        Assert.True(deleted >= 1);
        Assert.False(await db.AuditLogs.AnyAsync(a => a.Id == old.Id));
    }

    [SkippableFact]
    public async Task Published_consent_texts_cannot_be_changed_or_deleted()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();

        await AssertRejectedAsync(() => db.Database.ExecuteSqlRawAsync("UPDATE consent_texts SET body = 'değişti'"));
        await AssertRejectedAsync(() => db.Database.ExecuteSqlRawAsync("DELETE FROM consent_texts"));
        await AssertRejectedAsync(() => db.Database.ExecuteSqlRawAsync("TRUNCATE consent_texts"));

        // SaveChanges yolu da kapalı (interceptor).
        var text = await db.ConsentTexts.FirstAsync();
        db.ConsentTexts.Remove(text);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [SkippableFact]
    public async Task Initial_consent_texts_come_with_the_migration()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var service = new ConsentTextService(db);

        var biometric = await service.GetCurrentAsync(ConsentType.Biometric);
        var session = await service.GetCurrentAsync(ConsentType.Session);

        Assert.Equal(ConsentText.InitialBiometricVersion, biometric?.Version);
        Assert.Equal(ConsentText.InitialSessionVersion, session?.Version);
        Assert.Contains("30 gün", biometric!.Body);
        Assert.Contains("saklama süresi", session!.Body);
    }

    private static async Task AssertRejectedAsync(Func<Task> action)
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(action);
        Assert.Contains("yalnızca eklemeli", ex.MessageText);
    }
}
