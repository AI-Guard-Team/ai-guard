using AIGuard.Application.Abstractions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Agents;

public enum IssuePairingCodeStatus
{
    Issued,
    SessionNotFound,

    /// <summary>Oturum <see cref="SessionState.AgentPairing"/> durumunda değil (kart: kod yalnızca bu durumda üretilir).</summary>
    NotInPairingState,
}

public sealed record IssuePairingCodeResult(IssuePairingCodeStatus Status, string? Code = null, DateTimeOffset? ExpiresAt = null);

public enum PairAgentStatus
{
    Paired,

    /// <summary>Kod yok, süresi dolmuş, daha önce kullanılmış, oturum kapalı ya da işletim sistemi bildirilmemiş.</summary>
    Rejected,
}

public sealed record PairAgentResult(PairAgentStatus Status, Guid? PairingId = null, Guid? SessionId = null);

/// <summary>
/// Tarayıcı ile ajanın aynı bilgisayarda çalıştığının doğrulanması (Y1, FR-11).
/// </summary>
/// <remarks>
/// 1. Tarayıcı <see cref="IssueCodeAsync"/> ile kod alır (adaya gösterilmez).
/// 2. Tarayıcı kodu http://127.0.0.1:47321/pair adresindeki ajana gönderir.
/// 3. Ajan kodu TLS ile POST /api/agent/pair ucuna iletir → <see cref="PairAsync"/>.
/// Kod yalnızca loopback üzerinden taşındığı için onu sunucuya getirebilen ajan, tarayıcıyla
/// aynı bilgisayardadır.
/// </remarks>
public sealed class AgentPairingService(IAppDbContext db, TimeProvider clock)
{
    /// <summary>
    /// Kartta önerilen süre (5 dk). Tarayıcı kodu hemen ajana gönderiyor; ajan yeni açılıyorsa
    /// birkaç deneme için pay kalıyor. Kısa süre, sızan bir kodun işe yarama aralığını daraltıyor.
    /// </summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);

    private static readonly SessionState[] ClosedStates =
        [SessionState.Completed, SessionState.Cancelled, SessionState.Decided];

    public async Task<IssuePairingCodeResult> IssueCodeAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.ExamSessions
            .Include(s => s.AgentPairing)
            .SingleOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null)
        {
            return new(IssuePairingCodeStatus.SessionNotFound);
        }

        // Kod yalnızca eşleştirme adımında üretiliyor (UC-08 adım 1). Sınav başladıktan sonra yeni
        // kod alınıp oturuma başka bir ajan bağlanamasın.
        if (session.State != SessionState.AgentPairing)
        {
            return new(IssuePairingCodeStatus.NotInPairingState);
        }

        var code = PairingCode.Generate();
        var expiresAt = clock.GetUtcNow() + CodeLifetime;

        if (session.AgentPairing is null)
        {
            // Açıkça Add: Id baştan dolu olduğu için (Entity) navigasyonla bağlanırsa EF kaydı
            // var sanıp INSERT yerine UPDATE çalıştırıyor.
            db.AgentPairings.Add(new AgentPairing
            {
                ExamSessionId = session.Id,
                PairingCodeHash = PairingCode.Hash(code),
                PairingCodeExpiresAt = expiresAt,
            });
        }
        else
        {
            // Oturum başına tek eşleşme kaydı var (bire bir ilişki). Yeni kod istenirse (ör. ajan
            // yeniden başlatıldı) aynı kayıt güncelleniyor; önceki kod bu anda geçersiz oluyor.
            session.AgentPairing.PairingCodeHash = PairingCode.Hash(code);
            session.AgentPairing.PairingCodeExpiresAt = expiresAt;
        }

        await db.SaveChangesAsync(ct);
        return new(IssuePairingCodeStatus.Issued, code, expiresAt);
    }

    public async Task<PairAgentResult> PairAsync(string? code, AgentPlatform? platform, CancellationToken ct = default)
    {
        // Bütün red nedenleri aynı sonucu dönüyor: kodun var olup olmadığı dışarıya sızmasın.
        var rejected = new PairAgentResult(PairAgentStatus.Rejected);

        // İşletim sistemi zorunlu: ajan değerlendirmesi (Y7a, Y7b) sonuçları buna göre ayırıyor.
        if (!PairingCode.IsWellFormed(code) || platform is not { } os || !Enum.IsDefined(os))
        {
            return rejected;
        }

        var hash = PairingCode.Hash(code!);
        var now = clock.GetUtcNow();

        // İzlemeden (AsNoTracking) yalnızca oturum kimliğini alıyoruz; kayıt kilitten sonra okunacak.
        var sessionId = await db.AgentPairings
            .AsNoTracking()
            .Where(p => p.PairingCodeHash == hash)
            .Select(p => (Guid?)p.ExamSessionId)
            .SingleOrDefaultAsync(ct);

        if (sessionId is null)
        {
            return rejected;
        }

        // Tek kullanım: aynı kodla iki istek aynı anda gelirse ikisi de yukarıdaki sorguda kaydı
        // bulabilir. Oturum satırı kilitlenince (K-18) istekler sıraya giriyor; ikincisi aşağıdaki
        // sorguda birincinin tükettiği kodu görüp reddediliyor.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockExamSessionAsync(sessionId.Value, ct);

        var pairing = await db.AgentPairings
            .Include(p => p.ExamSession)
            .SingleOrDefaultAsync(p => p.ExamSessionId == sessionId && p.PairingCodeHash == hash, ct);

        if (pairing is null
            || pairing.PairingCodeExpiresAt <= now
            || ClosedStates.Contains(pairing.ExamSession.State))
        {
            return rejected;
        }

        // Süreyi "şimdi"ye çekmek kodu geçersiz kılıyor; şemada ayrı bir "kullanıldı" alanı gerekmiyor.
        pairing.PairedAt = now;
        pairing.Platform = os;
        pairing.PairingCodeExpiresAt = now;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new(PairAgentStatus.Paired, pairing.Id, pairing.ExamSessionId);
    }
}
