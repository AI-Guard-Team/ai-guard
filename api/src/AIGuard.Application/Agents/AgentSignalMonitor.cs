using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Events;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Agents;

/// <summary>
/// Sinyal kesintisi ve biten oturumların anahtarları (Y2). API içinde birkaç saniyede bir çalışıyor
/// (Api/Agents/AgentSignalWatchdog); mantık burada ki testte saat ileri alınarak sınanabilsin.
/// </summary>
public sealed class AgentSignalMonitor(IAppDbContext db, IAgentKeyStore keys, EventService events, TimeProvider clock)
{
    /// <summary>Kart: 30 saniye sinyal gelmezse "ajan sinyali kesildi".</summary>
    public static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(30);


    /// <returns>Yazılan AGENT_SIGNAL_LOST olayı sayısı.</returns>
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        await RevokeEndedSessionsAsync(ct);
        return await RecordLostSignalsAsync(ct);
    }

    private async Task RevokeEndedSessionsAsync(CancellationToken ct)
    {
        // Kart: oturum bitince (Completed, Cancelled) anahtar geçersiz. Oturumu bitiren kod (E4)
        // bunu bilmese de anahtar en geç bir tur sonra siliniyor.
        var ended = await db.AgentPairings
            .Where(p => p.SessionKeyHash != null && (p.ExamSession.State == SessionState.Completed || p.ExamSession.State == SessionState.Cancelled || p.ExamSession.State == SessionState.Decided))
            .ToListAsync(ct);

        foreach (var pairing in ended)
        {
            keys.Remove(pairing.ExamSessionId);
            pairing.SessionKeyHash = null;
        }

        if (ended.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<int> RecordLostSignalsAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var deadline = now - SignalTimeout;

        // Son geçerli sinyal (hiç yoksa eşleşme anı) 30 sn'den eskiyse ve o andan sonra henüz
        // AGENT_SIGNAL_LOST yazılmamışsa: her kesinti için tek olay, her turda yeni olay değil.
        var silent = await db.AgentPairings
            .AsNoTracking()
            .Where(p => p.PairedAt != null && !(p.ExamSession.State == SessionState.Completed || p.ExamSession.State == SessionState.Cancelled || p.ExamSession.State == SessionState.Decided))
            .Select(p => new { p.ExamSessionId, Last = p.LastSignalAt ?? p.PairedAt!.Value })
            .Where(p => p.Last <= deadline)
            .Where(p => !db.Events.Any(e => e.ExamSessionId == p.ExamSessionId
                                            && e.Type == EventType.AGENT_SIGNAL_LOST
                                            && e.StartedAt >= p.Last))
            .ToListAsync(ct);

        var recorded = 0;
        foreach (var s in silent)
        {
            // Olayın başlangıcı son sinyalin anı: değerlendirici kesintinin ne zaman başladığını görsün.
            var result = await events.RecordSystemEventAsync(s.ExamSessionId, new SystemEvent(
                EventType.AGENT_SIGNAL_LOST, s.Last,
                Payload: JsonSerializer.Serialize(new { lastSignalAt = s.Last })), ct);
            if (result.Status == EventRecordStatus.Recorded)
            {
                recorded++;
            }
        }

        return recorded;
    }
}
