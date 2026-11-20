using AIGuard.Domain.Common;
using AIGuard.Domain.Enums;

namespace AIGuard.Domain.Entities;

/// <summary>Açık rıza kaydı. Geri alındığında kayıt silinmiyor, WithdrawnAt dolduruluyor.</summary>
public class ConsentRecord : Entity
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Yalnızca oturum rızasında dolu.</summary>
    public Guid? ExamSessionId { get; set; }

    public ExamSession? ExamSession { get; set; }

    public ConsentType Type { get; set; }

    /// <summary>Onaylanan metnin sürümü, örn. "kvkk-biometric-v1".</summary>
    public required string TextVersion { get; set; }

    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? WithdrawnAt { get; set; }
}

/// <summary>
/// Adayın referans yüz şablonu. Yalnızca şifreli vektör tutuluyor; ham fotoğraf vektör
/// çıkarıldıktan sonra siliniyor (A3). Bu sınıfa görüntü alanı eklenmemeli, testler de
/// bunu kontrol ediyor.
/// </summary>
public class ReferenceFaceTemplate : Entity
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>AES-GCM ile şifrelenmiş ArcFace vektörü.</summary>
    public required byte[] EncryptedVector { get; set; }

    /// <summary>Anahtar değiştirildiğinde eski kayıtları açabilmek için.</summary>
    public int KeyVersion { get; set; } = 1;

    public Guid ConsentRecordId { get; set; }

    public ConsentRecord ConsentRecord { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Kimlik doğrulama denemesi. Başarısız denemeler de kaydediliyor.</summary>
public class Verification : Entity
{
    public Guid ExamSessionId { get; set; }

    public ExamSession ExamSession { get; set; } = null!;

    public VerificationKind Kind { get; set; }

    public double LivenessScore { get; set; }

    public double Similarity { get; set; }

    /// <summary>Görüntü servisinin kararı; eşikler servisin config.yaml dosyasında (S-19).</summary>
    public bool Passed { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
