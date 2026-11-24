using AIGuard.Domain.Enums;

namespace AIGuard.Application.Review;

/// <summary>İnceleme uçlarını çağıran kullanıcı. Admin okuyabilir, karar ve not yalnızca atanmış değerlendiricide (K-31).</summary>
public sealed record ReviewActor(Guid UserId, UserRole Role);

/// <summary>
/// Oturumun inceleme açısından durumu (K-31). Ekran rozeti ve karar düğmesi buna göre.
/// </summary>
public enum ReviewStatus
{
    /// <summary>Rıza, eşleştirme veya doğrulamada kalıp son başlangıç zamanını kaçırdı (S-16). Karar verilemez.</summary>
    NotStarted,

    /// <summary>Riskli veya işaretli tamamlandı, karar bekliyor (FR-27, FR-29).</summary>
    AwaitingReview,

    /// <summary>Tamamlandı, hiçbir işaret yok; karar gerekmiyor.</summary>
    Completed,

    /// <summary>Değerlendirici karar verdi.</summary>
    Decided,
}

public enum ReviewSort
{
    /// <summary>Varsayılan: karar bekleyenler, sonra işaretliler, sonra skor (kart: "riskli ve işaretliler önde").</summary>
    Priority,
    Score,
    StartedAt,
}

/// <param name="Risky">true: yalnızca riskli; false: yalnızca riskli olmayan.</param>
/// <param name="Flags">Bu işaretlerden en az biri olanlar.</param>
public sealed record ReviewListQuery(bool? Risky = null, IReadOnlyList<SessionFlags>? Flags = null, ReviewSort Sort = ReviewSort.Priority);

/// <param name="StartedAt">Sınava giriş zamanı; sınav hiç başlamadıysa oturumun açıldığı an.</param>
/// <param name="TopReason">Skora en çok katkı yapan olayın gerekçesi; yanlış alarmlar hariç.</param>
public sealed record ReviewSessionRow(
    Guid SessionId,
    Guid CandidateId,
    string CandidateName,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    double RiskScore,
    IReadOnlyList<SessionFlags> Flags,
    ReviewStatus Status,
    ReviewDecisionType? Decision,
    string? TopReason);

/// <param name="PendingCount">Bu sınavda karar bekleyen oturum sayısı (FR-29).</param>
public sealed record ReviewSessionList(int PendingCount, IReadOnlyList<ReviewSessionRow> Sessions);

/// <param name="Contribution">Sınav bitişindeki katkı (azalma, tekrar ve tür üst sınırı dahil); yanlış alarmda 0.</param>
/// <param name="EvidenceUrl">Kısa ömürlü imzalı bağlantı; kare yoksa ya da silindiyse <c>null</c>.</param>
public sealed record ReviewEventView(
    Guid Id,
    EventType Type,
    EventSource Source,
    DateTimeOffset StartedAt,
    double DurationSec,
    double? Confidence,
    string? Reason,
    double Contribution,
    bool IsFalseAlarm,
    string? FalseAlarmMarkedBy,
    DateTimeOffset? FalseAlarmMarkedAt,
    string? EvidenceUrl);

public sealed record ScorePoint(DateTimeOffset At, double Score);

public sealed record ReviewDecisionView(ReviewDecisionType Decision, string? Note, string ReviewerName, DateTimeOffset DecidedAt);

public sealed record ReviewNoteView(Guid Id, string Text, string AuthorName, DateTimeOffset CreatedAt);

public sealed record ReviewSessionDetail(
    Guid SessionId,
    Guid ExamId,
    string ExamName,
    Guid CandidateId,
    string CandidateName,
    SessionState State,
    ReviewStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    double RiskScore,
    IReadOnlyList<SessionFlags> Flags,
    ReviewDecisionView? Decision,
    IReadOnlyList<ReviewNoteView> Notes,
    IReadOnlyList<ReviewEventView> Events,
    IReadOnlyList<ScorePoint> ScoreHistory);

public sealed record DecisionRequest(ReviewDecisionType? Decision, string? Note);

public sealed record NoteRequest(string? Text);

public enum ReviewOperationStatus
{
    Success,
    NotFound,
    Invalid,
    Conflict,
}

public sealed record ReviewOperationResult(ReviewOperationStatus Status, string? Error = null)
{
    public static readonly ReviewOperationResult Ok = new(ReviewOperationStatus.Success);
    public static readonly ReviewOperationResult NotFound = new(ReviewOperationStatus.NotFound);

    public static ReviewOperationResult Invalid(string error) => new(ReviewOperationStatus.Invalid, error);

    public static ReviewOperationResult Conflict(string error) => new(ReviewOperationStatus.Conflict, error);
}

/// <summary>Aday bazında sınav sonucu. Bir soruda en yüksek seq'li cevap geçerli (E4).</summary>
/// <param name="Unmonitored">Bağlantı kesikken verilen ("izlenmeyen sürede") geçerli cevap sayısı (FR-26).</param>
public sealed record ExamResultView(int QuestionCount, int Correct, int Wrong, int Blank, int Unmonitored);

public sealed record EventTypeSummary(EventType Type, int Count, int FalseAlarms, double Contribution);

/// <summary>Aday bazında sınav raporu (E6'daki rapor sayfası). Kanıt kareleri ve zaman çizelgesi detay ucunda.</summary>
public sealed record CandidateReport(
    Guid SessionId,
    Guid ExamId,
    string ExamName,
    ExamType ExamType,
    int DurationMinutes,
    Guid CandidateId,
    string CandidateName,
    string CandidateEmail,
    SessionState State,
    ReviewStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    ExamResultView? Result,
    double RiskScore,
    IReadOnlyList<SessionFlags> Flags,
    IReadOnlyList<string> TopReasons,
    IReadOnlyList<EventTypeSummary> Events,
    int VerificationAttempts,
    ReviewDecisionView? Decision,
    IReadOnlyList<ReviewNoteView> Notes);
