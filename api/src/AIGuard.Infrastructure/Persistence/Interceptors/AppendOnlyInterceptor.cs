using AIGuard.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AIGuard.Infrastructure.Persistence.Interceptors;

/// <summary>
/// <see cref="IAppendOnly"/> kayıtların (denetim kaydı, rıza metinleri) güncellenmesini ve silinmesini
/// SaveChanges sırasında engelliyor (K-10). ExecuteUpdate/ExecuteDelete ve ham SQL buraya uğramıyor; onları
/// veritabanındaki tetikleyiciler durduruyor (K-32).
/// </summary>
public sealed class AppendOnlyInterceptor : SaveChangesInterceptor
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

        var violation = context.ChangeTracker.Entries<IAppendOnly>()
            .FirstOrDefault(e => e.State is EntityState.Modified or EntityState.Deleted);

        if (violation is not null)
        {
            throw new InvalidOperationException(
                $"{violation.Entity.GetType().Name} kayıtları değiştirilemez veya silinemez (istenen işlem: {violation.State}).");
        }
    }
}
