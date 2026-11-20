using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Agents;

/// <summary>GET /api/sessions/{id}/agent: ajan eşleştirme adımında adaya gösterilecek durum (Y4).</summary>
/// <param name="AgentActive">Ajan eşleşmiş ve son 30 sn içinde geçerli sinyal göndermiş.</param>
/// <param name="Indicators">Son geçerli sinyaldeki engelleyici göstergeler, neden ve çözüm adımıyla. Hipervizör bilgisi burada yok (UC-08 4b).</param>
public sealed record AgentStatusView(
    SessionState State,
    bool Paired,
    bool AgentActive,
    DateTimeOffset? LastSignalAt,
    IReadOnlyList<IndicatorAdviceView> Indicators,
    bool CanContinueIndicated,
    bool CanContinueWithoutAgent);

/// <summary>POST /api/sessions/{id}/agent/continue gövdesi: <c>{ mode: "noAgent" | "indicated", reason }</c>.</summary>
public sealed record AgentContinueRequest(string? Mode, string? Reason);

public enum AgentContinueMode
{
    /// <summary>Ajan çalışmadı (FR-12): Flags += NoAgent, cihaz ve ağ denetimi yapılmaz.</summary>
    NoAgent,

    /// <summary>Ajan gösterge buldu (FR-20): Flags += Indicated, denetim sürer.</summary>
    Indicated,
}

public enum AgentContinueStatus
{
    Continued,
    NotFound,

    /// <summary>Gövde geçersiz: bilinmeyen mod ya da boş/uzun gerekçe (400).</summary>
    Invalid,

    /// <summary>Oturum ajan eşleştirme adımında değil (409).</summary>
    WrongState,

    /// <summary>Ajansız devam istendi ama ajan çalışıyor (409): göstergeli devam ya da gösterge giderilmeli.</summary>
    AgentActive,

    /// <summary>Göstergeli devam istendi ama engelleyici gösterge yok (409).</summary>
    NoIndicators,
}

public sealed record AgentContinueResult(AgentContinueStatus Status, string? Error = null);

/// <summary>
/// Ajansız ve göstergeli devam (Y4, FR-12, FR-20, FR-35, UC-08 4a/4b). Gerekçe oturuma yazılıyor,
/// işlem denetim kaydına giriyor, oturum kimlik doğrulamaya geçiyor. Ayrıntılar: K-25.
/// </summary>
public sealed class AgentContinueService(IAppDbContext db, IAgentKeyStore keys, TimeProvider clock)
{
    public const int MaxReasonLength = 2000;

    public const string AuditNoAgent = "session.continue_no_agent";
    public const string AuditIndicated = "session.continue_indicated";

    public async Task<AgentStatusView?> GetStatusAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.ExamSessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new { s.State })
            .SingleOrDefaultAsync(ct);

        return session is null ? null : await BuildStatusAsync(sessionId, session.State, ct);
    }

    public async Task<AgentContinueResult> ContinueAsync(
        Guid sessionId, Guid actorUserId, AgentContinueRequest request, CancellationToken ct = default)
    {
        if (ParseMode(request.Mode) is not { } mode)
        {
            return new(AgentContinueStatus.Invalid, "mode \"noAgent\" ya da \"indicated\" olmalı.");
        }

        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return new(AgentContinueStatus.Invalid, "Devam etmek için gerekçe yazın.");
        }

        if (reason.Length > MaxReasonLength)
        {
            return new(AgentContinueStatus.Invalid, $"Gerekçe en fazla {MaxReasonLength} karakter olabilir.");
        }

        // Ajan sinyali de oturumu IdentityVerification'a geçirebiliyor; ikisi aynı anda gelirse sıraya girsin (K-18).
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockExamSessionAsync(sessionId, ct);

        var session = await db.ExamSessions.SingleOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
        {
            return new(AgentContinueStatus.NotFound);
        }

        if (session.State != SessionState.AgentPairing)
        {
            return new(AgentContinueStatus.WrongState, "Oturum ajan eşleştirme adımında değil.");
        }

        var status = await BuildStatusAsync(sessionId, session.State, ct);

        if (mode == AgentContinueMode.NoAgent && !status.CanContinueWithoutAgent)
        {
            // Ajan çalışırken "ajansız" devam, cihaz denetimini kapatmanın yolu olmasın.
            return new(AgentContinueStatus.AgentActive, "Güvenlik ajanı çalışıyor; ajansız devam edilemez.");
        }

        if (mode == AgentContinueMode.Indicated && !status.CanContinueIndicated)
        {
            return new(AgentContinueStatus.NoIndicators, "Ajanın bildirdiği bir gösterge yok.");
        }

        var now = clock.GetUtcNow();
        session.ContinueJustification = reason;
        session.Flags |= mode == AgentContinueMode.NoAgent ? SessionFlags.NoAgent : SessionFlags.Indicated;
        session.TransitionTo(SessionState.IdentityVerification);

        if (mode == AgentContinueMode.NoAgent)
        {
            // FR-12: ajansız oturumda cihaz ve ağ denetimi yapılmıyor. Daha önce eşleşip susmuş bir ajan
            // varsa anahtarı geçersiz; sonradan gelen sinyal kabul edilmiyor, "sinyal kesildi" de yazılmıyor.
            var pairing = await db.AgentPairings.SingleOrDefaultAsync(p => p.ExamSessionId == sessionId, ct);
            if (pairing?.SessionKeyHash is not null)
            {
                pairing.SessionKeyHash = null;
            }

            keys.Remove(sessionId);
        }

        // FR-35: ajansız ve göstergeli devam denetim kaydına (K-10). Gerekçe adayın kendi yazdığı metin.
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = mode == AgentContinueMode.NoAgent ? AuditNoAgent : AuditIndicated,
            TargetType = nameof(ExamSession),
            TargetId = sessionId,
            NewValue = JsonSerializer.Serialize(new
            {
                mode = mode == AgentContinueMode.NoAgent ? "noAgent" : "indicated",
                reason,
                indicators = status.Indicators.Select(i => new { type = i.Type, name = i.Name }),
            }),
            OccurredAt = now,
        });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(AgentContinueStatus.Continued);
    }

    private async Task<AgentStatusView> BuildStatusAsync(Guid sessionId, SessionState state, CancellationToken ct)
    {
        var pairing = await db.AgentPairings.AsNoTracking()
            .Where(p => p.ExamSessionId == sessionId)
            .Select(p => new { p.Id, p.PairedAt, p.LastSignalAt, p.SessionKeyHash })
            .SingleOrDefaultAsync(ct);

        var paired = pairing?.PairedAt is not null && pairing.SessionKeyHash is not null;
        // Eşleşmeden hemen sonra ilk sinyal birkaç saniye sürebiliyor; o arada da ajan "çalışıyor" sayılıyor,
        // yoksa aday eşleşir eşleşmez ajansız devam seçip cihaz denetimini atlayabilirdi.
        var active = paired && clock.GetUtcNow() - (pairing!.LastSignalAt ?? pairing.PairedAt!.Value) <= AgentSignalMonitor.SignalTimeout;

        IReadOnlyList<IndicatorAdviceView> indicators = [];
        if (active)
        {
            var latest = await db.AgentSignals.AsNoTracking()
                .Where(s => s.AgentPairingId == pairing!.Id && s.SignatureValid)
                .OrderByDescending(s => s.ReceivedAt)
                .Select(s => s.Indicators)
                .FirstOrDefaultAsync(ct);

            indicators = AgentIndicator.ParseStored(latest)
                .Where(i => !i.IsInformational)
                .DistinctBy(i => i.Key)
                .Select(IndicatorAdvice.For)
                .ToList();
        }

        var pairingStep = state == SessionState.AgentPairing;
        return new AgentStatusView(
            state,
            paired,
            active,
            pairing?.LastSignalAt,
            indicators,
            CanContinueIndicated: pairingStep && active && indicators.Count > 0,
            CanContinueWithoutAgent: pairingStep && !active);
    }

    private static AgentContinueMode? ParseMode(string? mode) => mode switch
    {
        "noAgent" => AgentContinueMode.NoAgent,
        "indicated" => AgentContinueMode.Indicated,
        _ => null,
    };
}
