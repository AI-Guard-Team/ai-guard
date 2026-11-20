using AIGuard.Application.Abstractions;
using AIGuard.Domain.Enums;
using AIGuard.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Sessions;

/// <summary>
/// Sunucu saatine bağlı geçişler (E4): süresi dolan oturumu otomatik teslim eder (FR-09) ve bir süredir
/// istek gelmeyen oturumu bağlantı kesik sayar (FR-26). Api'deki arka plan görevi birkaç saniyede bir çağırır.
/// </summary>
public sealed class SessionTimekeeper(IAppDbContext db, TimeProvider clock)
{
    /// <summary>
    /// Bu süre boyunca adaydan istek gelmezse bağlantı kesik sayılıyor. Önyüz sınav sırasında en geç
    /// 10 sn'de bir oturum ucunu çağırmalı (K-24); kamera karesi geldiğinde (S4) bu doğal olarak sağlanıyor.
    /// </summary>
    public static readonly TimeSpan DisconnectAfter = TimeSpan.FromSeconds(30);

    // Bir turda işlenecek en fazla oturum; geri kalanlar sonraki turda.
    private const int BatchSize = 100;

    private static readonly SessionState[] Running = [.. SessionStateMachine.Running];

    public async Task<(int Completed, int Disconnected)> RunOnceAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();

        var due = await db.ExamSessions.AsNoTracking()
            .Where(s => Running.Contains(s.State) && s.EndsAt != null && s.EndsAt <= now)
            .Select(s => s.Id).Take(BatchSize).ToListAsync(ct);
        var completed = 0;
        foreach (var id in due)
        {
            // Teslim isteği aynı anda gelirse kilit sırayla işletiyor; ikinci gelen durumu değişmiş görüyor.
            completed += await WithLockAsync(id, session =>
            {
                if (!SessionStateMachine.Running.Contains(session.State) || session.EndsAt > now)
                {
                    return false;
                }

                // Sunucuda kayıtlı cevaplarla teslim; bitiş anı süre sonu (FR-26).
                SessionService.Complete(session, now);
                return true;
            }, ct) ? 1 : 0;
        }

        var threshold = now - DisconnectAfter;
        var stale = await db.ExamSessions.AsNoTracking()
            .Where(s => s.State == SessionState.InProgress && s.LastSeenAt != null && s.LastSeenAt < threshold)
            .Select(s => s.Id).Take(BatchSize).ToListAsync(ct);
        var disconnected = 0;
        foreach (var id in stale)
        {
            disconnected += await WithLockAsync(id, session =>
            {
                if (session.State != SessionState.InProgress || !(session.LastSeenAt < threshold))
                {
                    return false;
                }

                // Süre işlemeye devam ediyor; kesinti son istekten itibaren sayılıyor.
                session.TransitionTo(SessionState.Disconnected);
                session.DisconnectedAt = session.LastSeenAt;
                return true;
            }, ct) ? 1 : 0;
        }

        return (completed, disconnected);
    }

    private async Task<bool> WithLockAsync(Guid sessionId, Func<Domain.Entities.ExamSession, bool> change, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockExamSessionAsync(sessionId, ct);
        var session = await db.ExamSessions.FirstAsync(s => s.Id == sessionId, ct);
        if (!change(session))
        {
            return false;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}
