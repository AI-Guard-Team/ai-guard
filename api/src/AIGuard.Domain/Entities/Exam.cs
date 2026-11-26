using AIGuard.Domain.Common;
using AIGuard.Domain.Enums;

namespace AIGuard.Domain.Entities;

public class Exam : Entity
{
    public required string Name { get; set; }

    public ExamType Type { get; set; }

    public int DurationMinutes { get; set; }

    public DateTimeOffset WindowStartAt { get; set; }

    /// <summary>
    /// Erişim penceresinin bitişi. Son başlangıç zamanı (bitiş − süre) ayrı bir sütun
    /// olarak tutulmuyor, gerektiğinde hesaplanıyor.
    /// </summary>
    public DateTimeOffset WindowEndAt { get; set; }

    /// <summary>Kanıt kareleri ve olayların kaç gün saklanacağı.</summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Sınava özel oturum eşiği (K-49); boşsa riskconfig.json'daki platform eşiği. Olay başına ağırlık, azalma
    /// ve eşik <see cref="RiskSetting"/> satırlarında.
    /// </summary>
    public double? RiskSessionThreshold { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Question> Questions { get; set; } = new List<Question>();

    public ICollection<ExamAssignment> Assignments { get; set; } = new List<ExamAssignment>();

    public ICollection<ExamSession> Sessions { get; set; } = new List<ExamSession>();
}

public class Question : Entity
{
    public Guid ExamId { get; set; }

    public Exam Exam { get; set; } = null!;

    public required string Text { get; set; }

    public List<string> Options { get; set; } = new();

    /// <summary>
    /// Doğru seçeneğin <see cref="Options"/> içindeki sırası (0'dan başlar).
    /// Adaya giden yanıtlarda bulunmamalı; aday için ayrı DTO kullanılmalı.
    /// </summary>
    public int CorrectOptionIndex { get; set; }

    public int Order { get; set; }
}

public class ExamAssignment : Entity
{
    public Guid ExamId { get; set; }

    public Exam Exam { get; set; } = null!;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public AssignmentRole Role { get; set; }

    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Adayın tek oturum hakkını kullandığı an (GAB nesne modeli, FR-06). Rıza reddedilip
    /// oturum iptal edildiyse hak kullanılmış sayılmıyor (FR-10); bu alan E4'te oturum
    /// gerçekten başladığında dolduruluyor.
    /// </summary>
    public DateTimeOffset? AttemptUsedAt { get; set; }
}
