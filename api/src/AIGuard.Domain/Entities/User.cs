using AIGuard.Domain.Common;
using AIGuard.Domain.Enums;

namespace AIGuard.Domain.Entities;

/// <summary>Sistem kullanıcısı. Açık kayıt yok; kullanıcıları yönetici davet ediyor (E2).</summary>
public class User : Entity
{
    public required string FullName { get; set; }

    public required string Email { get; set; }

    /// <summary>
    /// Karşılaştırma ve benzersizlik için kullanılan e-posta.
    /// Her zaman <see cref="NormalizeEmail"/> ile üretilmeli.
    /// </summary>
    public required string NormalizedEmail { get; set; }

    /// <summary>Davet kabul edilene kadar boş.</summary>
    public string? PasswordHash { get; set; }

    public UserRole Role { get; set; }

    /// <summary>
    /// Pasife alınan kullanıcının belirteçleri sonraki istekte reddedilmeli,
    /// bu yüzden JWT'deki bilgiyle yetinmeyip bu alan da kontrol edilmeli.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Davet bağlantısındaki belirtecin kendisi değil SHA-256 özeti tutuluyor.
    public string? InviteTokenHash { get; set; }

    public DateTimeOffset? InviteExpiresAt { get; set; }

    public DateTimeOffset? InviteAcceptedAt { get; set; }

    // Art arda hatalı girişte geçici kilit (E2).
    public int FailedLoginCount { get; set; }

    public DateTimeOffset? LockoutEndAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ExamAssignment> Assignments { get; set; } = new List<ExamAssignment>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public ICollection<ConsentRecord> ConsentRecords { get; set; } = new List<ConsentRecord>();

    public ReferenceFaceTemplate? ReferenceFaceTemplate { get; set; }

    // ToLowerInvariant: Türkçe kültürde "I" → "ı" dönüşümü aynı adresi farklı gösterirdi.
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}

/// <summary>Yenileme belirteci. Tek kullanımlık; her kullanımda yenisi veriliyor (E2).</summary>
public class RefreshToken : Entity
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Belirtecin SHA-256 özeti; belirtecin kendisi saklanmıyor.</summary>
    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// Yerine verilen belirteç. İptal edilmiş bir belirteç tekrar kullanılırsa
    /// zincirdeki tüm belirteçleri iptal edebilmek için tutuluyor.
    /// </summary>
    public Guid? ReplacedByTokenId { get; set; }
}
