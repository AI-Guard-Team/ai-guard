using AIGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AIGuard.Infrastructure.Persistence.Interceptors;

/// <summary>
/// AuditLog kayıtlarının güncellenmesini ve silinmesini SaveChanges sırasında engelliyor.
/// ExecuteUpdate/ExecuteDelete ve ham SQL buraya uğramadığı için A5'te veritabanı
/// yetkisiyle ikinci bir koruma eklenecek (K-10).
/// </summary>
public sealed class AppendOnlyAuditLogInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        EnsureAppendOnly(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        EnsureAppendOnly(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void EnsureAppendOnly(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var violation = context.ChangeTracker.Entries<AuditLog>()
            .FirstOrDefault(e => e.State is EntityState.Modified or EntityState.Deleted);

        if (violation is not null)
        {
            throw new InvalidOperationException(
                $"AuditLog kayıtları değiştirilemez veya silinemez (istenen işlem: {violation.State}).");
        }
    }
}
