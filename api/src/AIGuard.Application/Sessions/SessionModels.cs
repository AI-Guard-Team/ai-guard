using AIGuard.Domain.Enums;

namespace AIGuard.Application.Sessions;

/// <summary>Cevap gövdesi (kart E4); önyüz (S3) her seçimde gönderiyor.</summary>
/// <param name="SelectedOption">Seçilen seçeneğin sırası (0'dan); null ise cevap temizlendi.</param>
/// <param name="Seq">Oturum içinde artan sıra numarası; bir soru için en yüksek seq geçerli.</param>
/// <param name="ClientTime">İstemcinin saati; güvenilmiyor, yalnızca kayıt için (kart).</param>
public sealed record AnswerRequest(Guid? QuestionId, int? SelectedOption, long? Seq, DateTimeOffset? ClientTime);

/// <summary>Adaya giden soru: doğru seçenek yok (E3, K-23).</summary>
public sealed record SessionQuestion(Guid Id, string Text, IReadOnlyList<string> Options, int Order);

/// <summary>Soru başına geçerli cevap (en yüksek seq).</summary>
public sealed record SessionAnswer(Guid QuestionId, int? SelectedOption, long Seq);

/// <summary>
/// Adayın oturum görünümü. Risk skoru ve işaretler yok: aday hangi davranışın puan getirdiğini görmemeli.
/// </summary>
/// <param name="EndsAt">Sunucu saatine göre bitiş; kalan süre bundan hesaplanmalı (istemci saati değil).</param>
/// <param name="ServerTime">Yanıtın üretildiği sunucu anı; istemci kendi saat farkını bununla düzeltebilir.</param>
/// <param name="Questions">Yalnızca InProgress'te dolu; izin beklerken sorular gizli (FR-17).</param>
/// <param name="LastSeq">Sunucunun aldığı en yüksek seq; sayfa yenilenince kuyruk buradan devam eder.</param>
public sealed record SessionView(
    Guid Id,
    Guid ExamId,
    string ExamName,
    SessionState State,
    int DurationMinutes,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndsAt,
    DateTimeOffset ServerTime,
    IReadOnlyList<SessionQuestion> Questions,
    IReadOnlyList<SessionAnswer> Answers,
    long LastSeq);

public enum StartSessionStatus
{
    /// <summary>Yeni oturum açıldı (ConsentPending).</summary>
    Created,

    /// <summary>Adayın açık bir oturumu vardı; aynısı döndü (sayfa yenilendi, tekrar "başla" basıldı).</summary>
    Resumed,

    /// <summary>Sınav yok ya da aday atanmamış.</summary>
    NotFound,

    /// <summary>Pencere dışında ya da hak kullanılmış; neden <see cref="StartSessionResult.Denial"/>'da.</summary>
    Denied,
}

public sealed record StartSessionResult(
    StartSessionStatus Status, Guid? SessionId = null, SessionState? State = null, Exams.StartDenial? Denial = null);

public enum SessionOperationStatus
{
    Success,

    /// <summary>Aynı seq daha önce kaydedilmiş (çevrimdışı kuyruk tekrar gönderdi); yeni kayıt yok.</summary>
    Duplicate,

    NotFound,
    Invalid,

    /// <summary>Oturumun durumu buna izin vermiyor ya da süre doldu.</summary>
    Conflict,
}

public sealed record SessionOperationResult(SessionOperationStatus Status, string? Error = null)
{
    public static readonly SessionOperationResult Ok = new(SessionOperationStatus.Success);
    public static readonly SessionOperationResult NotFound = new(SessionOperationStatus.NotFound);

    public static SessionOperationResult Invalid(string error) => new(SessionOperationStatus.Invalid, error);

    public static SessionOperationResult Conflict(string error) => new(SessionOperationStatus.Conflict, error);
}
