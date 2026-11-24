using AIGuard.Domain.Common;
using AIGuard.Domain.Enums;

namespace AIGuard.Domain.Entities;

/// <summary>Değerlendirici kararı. Oturum başına en fazla bir tane (A4).</summary>
public class ReviewerDecision : Entity
{
    public Guid ExamSessionId { get; set; }

    public ExamSession ExamSession { get; set; } = null!;

    public Guid ReviewerId { get; set; }

    public User Reviewer { get; set; } = null!;

    public ReviewDecisionType Decision { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset DecidedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Değerlendiricinin oturuma yazdığı not (A4, S-1). Bir oturuma birden fazla not yazılabiliyor;
/// karar verilmeden önce de yazılabildiği için karardaki tek not alanından ayrı tutuluyor.
/// Notlar düzenlenmiyor ve silinmiyor: inceleme geçmişinin parçası.
/// </summary>
public class ReviewerNote : Entity
{
    /// <summary>Not ve karar notu için üst sınır (karakter).</summary>
    public const int MaxLength = 4000;

    public Guid ExamSessionId { get; set; }

    public ExamSession ExamSession { get; set; } = null!;

    public Guid AuthorId { get; set; }

    public User Author { get; set; } = null!;

    public required string Text { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Denetim kaydı. Yalnızca ekleniyor: özellikler init, güncelleme ve silme AppendOnlyInterceptor ve
/// veritabanındaki tetikleyici tarafından reddediliyor (K-10). Tek istisna, 1 yılı dolan kayıtların
/// saklama görevince silinmesi (K-32). Eski/yeni değer alanlarına şifre, belirteç veya anahtar yazılmamalı.
/// </summary>
public class AuditLog : Entity, IAppendOnly
{
    /// <summary>Sistem görevlerinde boş.</summary>
    public Guid? ActorUserId { get; init; }

    public User? ActorUser { get; init; }

    /// <summary>Örn. "user.login", "consent.withdraw".</summary>
    public required string Action { get; init; }

    public string? TargetType { get; init; }

    public Guid? TargetId { get; init; }

    public string? OldValue { get; init; }

    public string? NewValue { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
