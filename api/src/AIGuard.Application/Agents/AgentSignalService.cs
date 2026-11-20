using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Events;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Agents;

/// <summary>POST /api/agent/signal gövdesi (kart Y2).</summary>
/// <param name="Ts">Ajanın imzaladığı an, Unix saniye.</param>
/// <param name="Indicators">Eşleşen göstergeler (Y3). Tam süreç listesi değil.</param>
/// <param name="Sig">HMAC-SHA256 imzası, Base64 (<see cref="AgentSignature"/>).</param>
public sealed record AgentSignalRequest(Guid SessionId, long Ts, string? Nonce, IReadOnlyList<string>? Indicators, string? Sig);

public enum AgentSignalStatus
{
    /// <summary>İmza, zaman ve nonce geçerli; sinyal kaydedildi.</summary>
    Accepted,

    /// <summary>Gövde biçimi bozuk; hangi oturuma ait olduğu güvenilir olmadığı için olay yazılmıyor.</summary>
    Malformed,

    /// <summary>Oturum yok ya da ajan eşleşmemiş.</summary>
    NotFound,

    /// <summary>Oturum bitmiş; anahtar geçersiz kılındı, ajan sinyal göndermeyi bırakmalı.</summary>
    SessionEnded,

    /// <summary>İmza bozuk, sinyal gecikmiş ya da tekrar gönderilmiş (AGENT_SIGNAL_INVALID yazıldı).</summary>
    Rejected,

    /// <summary>Anahtar API belleğinde yok (ör. API yeniden başladı); imza doğrulanamıyor.</summary>
    KeyUnavailable,
}

/// <summary>Neden reddedildi; olay ayrıntısına yazılıyor, yanıtta ajana ayrılmıyor.</summary>
public enum AgentSignalRejection
{
    Signature,
    Replay,
    Stale,
}

public sealed record AgentSignalResult(AgentSignalStatus Status, AgentSignalRejection? Rejection = null);

/// <summary>
/// İmzalı ajan sinyallerinin doğrulanması (Y2, FR-21, UC-08 adım 3–5). Ayrıntılar: K-21.
/// </summary>
public sealed class AgentSignalService(IAppDbContext db, IAgentKeyStore keys, EventService events, TimeProvider clock)
{
    /// <summary>Ajan saatiyle sunucu saati arasındaki izin verilen fark (kart: ±30 sn).</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromSeconds(30);

    public const int MaxIndicators = 32;
    public const int MaxIndicatorLength = 64;

    private static readonly SessionState[] ClosedStates =
        [SessionState.Completed, SessionState.Cancelled, SessionState.Decided];

    public async Task<AgentSignalResult> ProcessAsync(AgentSignalRequest request, CancellationToken ct = default)
    {
        var indicators = request.Indicators ?? [];
        if (!IsWellFormed(request, indicators))
        {
            return new(AgentSignalStatus.Malformed);
        }

        var pairing = await db.AgentPairings
            .AsNoTracking()
            .Where(p => p.ExamSessionId == request.SessionId && p.PairedAt != null)
            .Select(p => new { p.Id, p.SessionKeyHash, p.ExamSession.State })
            .SingleOrDefaultAsync(ct);

        if (pairing is null)
        {
            return new(AgentSignalStatus.NotFound);
        }

        // Kart: oturum bitince (Completed, Cancelled) anahtar geçersiz; o anahtarla gelen sinyal kabul edilmez.
        if (ClosedStates.Contains(pairing.State))
        {
            await RevokeAsync(request.SessionId, ct);
            return new(AgentSignalStatus.SessionEnded);
        }

        // Bellekteki anahtar, veritabanındaki özetle eşleşmeli: yeniden eşleşme sonrası eski anahtar
        // ya da başka bir oturumun anahtarı kullanılamaz.
        var key = keys.Get(request.SessionId);
        if (key is null || pairing.SessionKeyHash is null || AgentSignature.HashKey(key) != pairing.SessionKeyHash)
        {
            // Doğrulanamayan sinyal için olay yazılmıyor: büyük olasılıkla API yeniden başladı ve ajan
            // suçsuz. Sinyaller durduğu için 30 sn sonra AGENT_SIGNAL_LOST yine üretiliyor (K-21).
            return new(AgentSignalStatus.KeyUnavailable);
        }

        var now = clock.GetUtcNow();

        // Aynı oturuma gelen sinyaller ve olaylar sıraya giriyor (K-18). Aynı nonce ile eşzamanlı
        // iki istekten ikincisi, birincinin kaydettiği nonce'u görüp tekrar sayılıyor.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockExamSessionAsync(request.SessionId, ct);

        var rejection = Check(key, request, indicators, now)
            ?? (await db.AgentSignals.AnyAsync(s => s.AgentPairingId == pairing.Id && s.Nonce == request.Nonce, ct)
                ? AgentSignalRejection.Replay
                : null);

        // Tekrar gönderilen sinyal ikinci kez saklanamıyor: (eşleşme, nonce) benzersiz indeksi (A1).
        // Diğer bütün sinyaller, geçersiz olanlar dahil, AgentSignal olarak kaydediliyor (kart).
        if (rejection != AgentSignalRejection.Replay)
        {
            db.AgentSignals.Add(new AgentSignal
            {
                AgentPairingId = pairing.Id,
                SignedAt = DateTimeOffset.FromUnixTimeSeconds(request.Ts),
                Nonce = request.Nonce!,
                ReceivedAt = now,
                // Yalnızca kabul edilen sinyal "geçerli": gecikmiş sinyalin imzası doğru olsa da kabul edilmiyor.
                SignatureValid = rejection is null,
                Indicators = JsonSerializer.Serialize(indicators),
            });
        }

        if (rejection is { } reason)
        {
            await db.SaveChangesAsync(ct);
            await events.RecordSystemEventAsync(request.SessionId, new SystemEvent(
                EventType.AGENT_SIGNAL_INVALID, now,
                Payload: JsonSerializer.Serialize(new { reason = reason.ToString().ToLowerInvariant() })), ct);
            await transaction.CommitAsync(ct);
            return new(AgentSignalStatus.Rejected, reason);
        }

        var tracked = await db.AgentPairings.Include(p => p.ExamSession).SingleAsync(p => p.Id == pairing.Id, ct);
        tracked.LastSignalAt = now;

        // UC-08 adım 4–5: ilk geçerli sinyalde gösterge yoksa kimlik doğrulamaya geçiliyor. Gösterge
        // varsa adayın gerekçeyle devam etmesi Y4'te. Durum makinesi (E4) gelince geçiş oraya taşınmalı.
        if (tracked.ExamSession.State == SessionState.AgentPairing && indicators.Count == 0)
        {
            tracked.ExamSession.State = SessionState.IdentityVerification;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(AgentSignalStatus.Accepted);
    }

    /// <summary>
    /// Oturumun anahtarını geçersiz kılar: bellekten silinir, özet boşaltılır. Oturumu bitiren kod
    /// (E4) çağırmalı; çağrılmasa da <see cref="AgentSignalMonitor"/> birkaç saniye içinde yapıyor.
    /// </summary>
    public async Task RevokeAsync(Guid sessionId, CancellationToken ct = default)
    {
        keys.Remove(sessionId);
        var pairing = await db.AgentPairings.SingleOrDefaultAsync(p => p.ExamSessionId == sessionId, ct);
        if (pairing?.SessionKeyHash is not null)
        {
            pairing.SessionKeyHash = null;
            await db.SaveChangesAsync(ct);
        }
    }

    private static AgentSignalRejection? Check(byte[] key, AgentSignalRequest r, IReadOnlyList<string> indicators, DateTimeOffset now)
    {
        // Önce imza: imzası bozuk bir istekteki zaman damgasına güvenilemez.
        if (!AgentSignature.Verify(key, r.SessionId, r.Ts, r.Nonce!, indicators, r.Sig))
        {
            return AgentSignalRejection.Signature;
        }

        var signedAt = DateTimeOffset.FromUnixTimeSeconds(r.Ts);
        return (now - signedAt).Duration() > MaxClockSkew ? AgentSignalRejection.Stale : null;
    }

    private static bool IsWellFormed(AgentSignalRequest r, IReadOnlyList<string> indicators) =>
        r.SessionId != Guid.Empty
        // Unix saniye; uç değerler DateTimeOffset'i taşırmasın.
        && r.Ts is > 0 and < 253402300799
        && r.Nonce is { Length: >= 16 and <= 128 } nonce
        && nonce.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
        && !string.IsNullOrEmpty(r.Sig) && r.Sig.Length <= 64
        && indicators.Count <= MaxIndicators
        && indicators.All(i => i is { Length: > 0 and <= MaxIndicatorLength });
}
