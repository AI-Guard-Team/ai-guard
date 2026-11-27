using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Domain.Entities;

namespace AIGuard.Application.Audit;

/// <summary>
/// Reddedilen istek (401 ya da 403) denetim kaydına (GAB 3.3.2 "Reddedilen istekler ve kritik işlemler denetim
/// kaydına yazılmalı", S-27). GAB istisna tanımlamadığı için her ret yazılıyor; tablonun büyümesini GAB'ın veri yaşam
/// döngüsündeki "denetim kayıtları 1 yıl sonra silinir" kuralı sınırlıyor (K-32).
/// </summary>
public sealed class AccessDeniedRecorder(IAppDbContext db, TimeProvider clock)
{
    public const string Action = "access.denied";

    /// <param name="userId">Kimliği doğrulanmış kullanıcı; 401'de boş (belirteç doğrulanmadığı için kimseye yazılmıyor).</param>
    /// <param name="status">401 (giriş yok ya da belirteç geçersiz) veya 403 (yetki yok).</param>
    /// <param name="route">Uç şablonu (ör. <c>api/review/sessions/{sessionId}</c>); yoksa istek yolu.</param>
    /// <param name="path">Gerçek istek yolu; hangi kayda erişilmek istendiği görülsün.</param>
    public async Task RecordAsync(Guid? userId, int status, string method, string route, string path, CancellationToken ct = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            Action = Action,
            TargetType = "endpoint",
            NewValue = JsonSerializer.Serialize(new { status, method, route, path }),
            OccurredAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
    }
}
