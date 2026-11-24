using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.UnitTests;

/// <summary>
/// Interceptor veritabanına gitmeden çalıştığı için bu testler PostgreSQL gerektirmiyor.
/// </summary>
public class AppendOnlyInterceptorTests
{
    private static AuditLog SampleLog() => new() { Action = "user.login" };

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public void SaveChanges_rejects_update_or_delete(EntityState state)
    {
        using var db = TestDbContextFactory.Create();
        db.Attach(SampleLog()).State = state;

        var ex = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Contains("AuditLog", ex.Message);
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task SaveChangesAsync_rejects_update_or_delete(EntityState state)
    {
        await using var db = TestDbContextFactory.Create();
        db.Attach(SampleLog()).State = state;

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public void Published_consent_texts_cannot_be_changed_or_deleted(EntityState state)
    {
        // A5 (K-32): yayımlanan metin değişmiyor; metin değişecekse yeni sürüm ekleniyor.
        using var db = TestDbContextFactory.Create();
        db.Attach(new ConsentText { Type = ConsentType.Biometric, Version = "v1", Title = "t", Body = "b" }).State = state;

        var ex = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Contains(nameof(ConsentText), ex.Message);
    }

    [Fact]
    public void Rejection_happens_even_when_other_entities_are_valid()
    {
        // Geçerli değişikliklerle birlikte gönderilse de denetim kaydı silinememeli.
        using var db = TestDbContextFactory.Create();
        db.Add(new Exam { Name = "x", DurationMinutes = 10 });
        db.Attach(SampleLog()).State = EntityState.Deleted;

        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }
}
