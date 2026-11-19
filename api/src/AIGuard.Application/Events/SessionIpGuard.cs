using System.Net;
using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Events;

/// <summary>
/// Oturum boyunca istemci IP'sinin değişip değişmediğini kontrol ediyor (A2, FR-22).
/// Oturumla ilgili her istekte çağrılmalı.
/// </summary>
/// <remarks>
/// IP, HttpContext.Connection.RemoteIpAddress'ten gelmeli. API bir proxy arkasındaysa
/// X-Forwarded-For yalnızca bilinen proxy'den gelirken dikkate alınmalı; aksi hâlde istemci
/// başlığı değiştirerek IP'sini gizleyebilir (E1b).
/// </remarks>
public sealed class SessionIpGuard(IAppDbContext db, EventService events, TimeProvider clock)
{
    public async Task CheckAsync(Guid sessionId, IPAddress? clientIp, CancellationToken ct = default)
    {
        if (clientIp is null)
        {
            return;
        }

        // ::ffff:1.2.3.4 ile 1.2.3.4 aynı adres sayılmalı.
        var ip = clientIp.IsIPv4MappedToIPv6 ? clientIp.MapToIPv4() : clientIp;

        var session = await db.ExamSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
        {
            return;
        }

        if (session.InitialIpAddress is null)
        {
            session.InitialIpAddress = ip;
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Aynı anda başka bir istek ilk IP'yi yazdıysa onunki geçerli; sonraki istek karşılaştırır.
            }

            return;
        }

        if (session.InitialIpAddress.Equals(ip))
        {
            return;
        }

        // Aynı yeni IP için her istekte tekrar olay üretmemek adına son IP_CHANGE'e bakıyoruz.
        var lastChangePayload = await db.Events.AsNoTracking()
            .Where(e => e.ExamSessionId == sessionId && e.Type == EventType.IP_CHANGE)
            .OrderByDescending(e => e.ReceivedAt)
            .Select(e => e.Payload)
            .FirstOrDefaultAsync(ct);

        if (LastReportedIp(lastChangePayload) == ip.ToString())
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new { from = session.InitialIpAddress.ToString(), to = ip.ToString() });
        await events.RecordSystemEventAsync(sessionId, new SystemEvent(EventType.IP_CHANGE, clock.GetUtcNow(), Payload: payload), ct);
    }

    private static string? LastReportedIp(string? payload)
    {
        if (payload is null)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.TryGetProperty("to", out var to) ? to.GetString() : null;
    }
}
