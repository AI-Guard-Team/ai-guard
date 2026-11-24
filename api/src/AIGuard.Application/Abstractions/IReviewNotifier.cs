using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;

namespace AIGuard.Application.Abstractions;

/// <summary>
/// "SessionReadyForReview" mesajının içeriği (A4). Aday adı gönderilmiyor; panel bildirimi alınca
/// listeyi yeniden çekiyor, kişisel veri gereğinden fazla taşınmıyor.
/// </summary>
public sealed record SessionReadyForReview(
    Guid SessionId,
    Guid ExamId,
    double RiskScore,
    IReadOnlyList<SessionFlags> Flags,
    DateTimeOffset? EndedAt)
{
    /// <summary>Oturum incelemeye düşüyorsa mesajı, düşmüyorsa <c>null</c> döner (FR-27: riskli veya işaretli).</summary>
    public static SessionReadyForReview? For(ExamSession session) =>
        session.State == SessionState.Completed && session.Flags != SessionFlags.None
            ? new(session.Id, session.ExamId, session.RiskScore, SessionFlagList.Of(session.Flags), session.EndedAt)
            : null;
}

/// <summary>
/// Riskli veya işaretli tamamlanan oturumu değerlendiricilere bildirir (A4, SignalR <c>/hubs/review</c>).
/// Teslim kaydedildikten sonra çağrılıyor; uygulama hata fırlatmamalı, bildirim gitmese de teslim geçerli.
/// </summary>
public interface IReviewNotifier
{
    Task SessionReadyForReviewAsync(SessionReadyForReview message, CancellationToken ct = default);
}

/// <summary>[Flags] değerini tek tek işaretlere ayırır; JSON'da "Risky, NoAgent" yerine liste dönsün diye.</summary>
public static class SessionFlagList
{
    private static readonly SessionFlags[] All =
        [.. Enum.GetValues<SessionFlags>().Where(f => f != SessionFlags.None)];

    public static IReadOnlyList<SessionFlags> Of(SessionFlags flags) => [.. All.Where(f => flags.HasFlag(f))];
}
