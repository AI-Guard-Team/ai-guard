using System.Net;
using AIGuard.Domain.Common;
using AIGuard.Domain.Enums;

namespace AIGuard.Domain.Entities;

/// <summary>
/// Adayın bir sınavdaki oturumu. Aday başına tek oturum kuralı veritabanında da korunuyor:
/// (ExamId, CandidateId) yalnızca iptal edilmemiş oturumlar arasında benzersiz. Rızayı
/// reddeden adayın oturumu iptal olur ve hakkı kullanılmış sayılmaz (FR-10), bu yüzden
/// yeniden başlayabiliyor.
/// </summary>
public class ExamSession : Entity
{
    public Guid ExamId { get; set; }

    public Exam Exam { get; set; } = null!;

    public Guid CandidateId { get; set; }

    public User Candidate { get; set; } = null!;

    /// <summary>Yalnızca E4'teki durum makinesi üzerinden değiştirilmeli.</summary>
    public SessionState State { get; set; } = SessionState.ConsentPending;

    public SessionFlags Flags { get; set; } = SessionFlags.None;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Sunucu saatine göre bitiş. Kalan süre ve otomatik teslim bu alana göre.</summary>
    public DateTimeOffset? EndsAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// Adayın tarayıcısından gelen son istek (E4). Sınav sürerken bu değer eskirse bağlantı kesik
    /// sayılıyor ve oturum Disconnected oluyor; o sırada gelen cevaplar "izlenmeyen" işaretleniyor.
    /// </summary>
    public DateTimeOffset? LastSeenAt { get; set; }

    /// <summary>Bağlantının kesildiği an; dönüşte kesinti süresi bundan hesaplanıp olay yazılıyor (FR-26).</summary>
    public DateTimeOffset? DisconnectedAt { get; set; }

    /// <summary>Oturumun ilk IP'si; değişirse IP_CHANGE olayı üretiliyor (A2).</summary>
    public IPAddress? InitialIpAddress { get; set; }

    /// <summary>Sunucuda hesaplanıyor, istemciden alınmıyor.</summary>
    public double RiskScore { get; set; }

    public int VerificationAttempts { get; set; }

    /// <summary>Ajansız veya göstergeli devam ederken adayın yazdığı gerekçe (Y4).</summary>
    public string? ContinueJustification { get; set; }

    /// <summary>
    /// PostgreSQL xmin sütunu. Otomatik teslim ile adayın teslim isteği aynı anda gelirse
    /// ikinci güncelleme hata alıyor, böylece biri diğerinin üzerine yazmıyor.
    /// </summary>
    public uint Version { get; set; }

    public ICollection<Answer> Answers { get; set; } = new List<Answer>();

    public ICollection<Event> Events { get; set; } = new List<Event>();

    public ICollection<Verification> Verifications { get; set; } = new List<Verification>();

    public ICollection<ConsentRecord> ConsentRecords { get; set; } = new List<ConsentRecord>();

    /// <summary>Ajansız devam edilen oturumlarda boş.</summary>
    public AgentPairing? AgentPairing { get; set; }

    public ReviewerDecision? ReviewerDecision { get; set; }
}

/// <summary>Cevap gönderimi. Her gönderim ayrı satır; bir soru için en yüksek Seq geçerli (E4).</summary>
public class Answer : Entity
{
    public Guid ExamSessionId { get; set; }

    public ExamSession ExamSession { get; set; } = null!;

    public Guid QuestionId { get; set; }

    public Question Question { get; set; } = null!;

    /// <summary>Boşsa aday cevabını temizlemiş demektir.</summary>
    public int? SelectedOption { get; set; }

    /// <summary>
    /// Oturum içinde artan sıra numarası. Çevrimdışı kuyruk aynı cevabı tekrar gönderirse
    /// (oturum, seq) benzersiz indeksi ikinci kaydı engelliyor.
    /// </summary>
    public long Seq { get; set; }

    /// <summary>İstemcinin bildirdiği zaman; yalnızca bilgi amaçlı.</summary>
    public DateTimeOffset? ClientTime { get; set; }

    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Bağlantı kesikken verilmiş cevap.</summary>
    public bool Unmonitored { get; set; }
}
