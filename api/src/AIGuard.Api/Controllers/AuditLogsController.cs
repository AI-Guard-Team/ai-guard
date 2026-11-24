using AIGuard.Api.Auth;
using AIGuard.Application.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

/// <summary>
/// Denetim kaydının görüntülenmesi (A5, FR-36, GAB 3.3.8 erişim matrisi: yalnızca yönetici, yalnızca okuma).
/// Kayıt değiştirilemediği için yazma ucu yok (K-10, K-32).
/// </summary>
[ApiController]
[Route("api/audit-logs")]
[Authorize(Policy = AccessPolicies.ReadAuditLog)]
public sealed class AuditLogsController(AuditLogService auditLogs) : ControllerBase
{
    /// <summary>
    /// En yeni kayıt önde, sayfalı. <c>action</c> işlem adının başıyla eşleşiyor (ör. <c>review.</c>,
    /// <c>retention.</c>). <c>from</c>/<c>to</c> ISO 8601 zaman.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<AuditLogPage>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? action, [FromQuery] Guid? actorUserId, [FromQuery] string? targetType, [FromQuery] Guid? targetId,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = AuditLogService.DefaultPageSize, CancellationToken ct = default)
    {
        // Kayıtlarda kullanıcı kimlikleri ve işlem ayrıntıları var; ara önbelleklerde kalmasın.
        Response.Headers.CacheControl = "no-store";
        var (result, error) = await auditLogs.ListAsync(
            new AuditLogQuery(action, actorUserId, targetType, targetId, from, to, page, pageSize), ct);
        return result is not null
            ? Ok(result)
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: "Geçersiz sorgu.", detail: error);
    }
}
