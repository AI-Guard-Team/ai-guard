using AIGuard.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Audit;

/// <param name="Action">İşlem adının başı; ör. <c>review.</c> bütün inceleme işlemlerini getirir.</param>
/// <param name="Page">1'den başlıyor.</param>
/// <param name="PageSize">En fazla <see cref="AuditLogService.MaxPageSize"/>.</param>
public sealed record AuditLogQuery(
    string? Action = null,
    Guid? ActorUserId = null,
    string? TargetType = null,
    Guid? TargetId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = AuditLogService.DefaultPageSize);

/// <param name="ActorName">Sistem görevlerinde (ör. saklama süresi) boş.</param>
/// <param name="OldValue">JSON metni.</param>
/// <param name="NewValue">JSON metni.</param>
public sealed record AuditLogView(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Action,
    Guid? ActorUserId,
    string? ActorName,
    string? TargetType,
    Guid? TargetId,
    string? OldValue,
    string? NewValue);

public sealed record AuditLogPage(int Total, int Page, int PageSize, IReadOnlyList<AuditLogView> Items);

/// <summary>
/// Denetim kaydının görüntülenmesi (A5, GAB 3.3.8 erişim matrisi: yalnızca yönetici, yalnızca okuma). Bu servis
/// kayda hiçbir şey yazmıyor; denetim kaydının kendisi değiştirilemez (K-10, K-32).
/// </summary>
public sealed class AuditLogService(IAppDbContext db)
{
    public const int DefaultPageSize = 50;

    public const int MaxPageSize = 200;

    /// <summary>En yeni kayıt önde. Geçersiz sorguda (sayfa, tarih aralığı) <c>Error</c> dolu döner.</summary>
    public async Task<(AuditLogPage? Page, string? Error)> ListAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > MaxPageSize)
        {
            return (null, $"page en az 1, pageSize 1–{MaxPageSize} olmalı.");
        }

        if (query.From > query.To)
        {
            return (null, "from, to'dan sonra olamaz.");
        }

        var logs = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            logs = logs.Where(a => a.Action.StartsWith(action));
        }

        if (query.ActorUserId is { } actor)
        {
            logs = logs.Where(a => a.ActorUserId == actor);
        }

        if (!string.IsNullOrWhiteSpace(query.TargetType))
        {
            logs = logs.Where(a => a.TargetType == query.TargetType);
        }

        if (query.TargetId is { } target)
        {
            logs = logs.Where(a => a.TargetId == target);
        }

        if (query.From is { } from)
        {
            logs = logs.Where(a => a.OccurredAt >= from);
        }

        if (query.To is { } to)
        {
            logs = logs.Where(a => a.OccurredAt <= to);
        }

        var total = await logs.CountAsync(ct);
        var items = await logs
            .OrderByDescending(a => a.OccurredAt).ThenBy(a => a.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new AuditLogView(
                a.Id, a.OccurredAt, a.Action, a.ActorUserId, a.ActorUser == null ? null : a.ActorUser.FullName,
                a.TargetType, a.TargetId, a.OldValue, a.NewValue))
            .ToListAsync(ct);

        return (new AuditLogPage(total, query.Page, query.PageSize, items), null);
    }
}
