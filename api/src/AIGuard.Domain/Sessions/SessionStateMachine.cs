using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;

namespace AIGuard.Domain.Sessions;

/// <summary>Tabloda olmayan bir durum geçişi denendi. API bunu 409 olarak döndürüyor (E4).</summary>
public sealed class InvalidSessionTransitionException(SessionState from, SessionState to)
    : InvalidOperationException($"{from} durumundan {to} durumuna geçiş tanımlı değil.")
{
    public SessionState From { get; } = from;

    public SessionState To { get; } = to;
}

/// <summary>
/// Sınav oturumunun yaşam döngüsü (E4, GAB 3.4.4). <see cref="ExamSession.State"/> yalnızca
/// <see cref="TransitionTo"/> ile değiştirilmeli (K-14, K-24); tabloda olmayan geçiş istisna fırlatıyor.
/// </summary>
/// <remarks>
/// GAB tablosundaki "aynı durumda kalır" satırları (ör. doğrulama başarısız, gösterge bulundu,
/// periyodik eşleştirme başarısız) durum değiştirmediği için burada yok; o olaylar yalnızca
/// kayıt üretiyor. Geçişin yan etkileri (süre başlatma, olay yazma) Application'daki SessionService'te.
/// </remarks>
public static class SessionStateMachine
{
    // (mevcut, yeni) çiftleri; GAB 3.4.4 ile birebir.
    private static readonly HashSet<(SessionState From, SessionState To)> Allowed =
    [
        // Rıza
        (SessionState.ConsentPending, SessionState.AgentPairing),         // açık rıza verildi
        (SessionState.ConsentPending, SessionState.Cancelled),            // rıza reddedildi; hak kullanılmış sayılmaz (FR-10)

        // Ajan eşleştirme
        (SessionState.AgentPairing, SessionState.IdentityVerification),   // eşleşme, gösterge yok / ajansız ya da göstergeli devam

        // Kimlik doğrulama
        (SessionState.IdentityVerification, SessionState.InProgress),     // canlılık ve eşleşme başarılı; süre başlar

        // Sınav sırasında
        (SessionState.InProgress, SessionState.PermissionPending),        // kamera/mikrofon izni kaldırıldı
        (SessionState.PermissionPending, SessionState.InProgress),        // izin yeniden verildi
        (SessionState.InProgress, SessionState.Disconnected),             // bağlantı kesildi / sayfadan çıkıldı
        (SessionState.Disconnected, SessionState.InProgress),             // süre içinde döndü, yeniden doğrulama başarılı

        // Bitiş
        (SessionState.InProgress, SessionState.Completed),                // teslim / süre doldu
        (SessionState.PermissionPending, SessionState.Completed),         // süre doldu
        (SessionState.Disconnected, SessionState.Completed),              // süre doldu

        // Biyometrik rıza sınav sırasında geri alındı (FR-05)
        (SessionState.InProgress, SessionState.Cancelled),
        (SessionState.PermissionPending, SessionState.Cancelled),
        (SessionState.Disconnected, SessionState.Cancelled),

        // Değerlendirici kararı; yalnızca riskli veya işaretli oturum için (kontrol A4'te)
        (SessionState.Completed, SessionState.Decided),
    ];

    /// <summary>Oturum kapanmış: cevap, olay ve sinyal kabul edilmiyor.</summary>
    public static readonly IReadOnlySet<SessionState> Closed =
        new HashSet<SessionState> { SessionState.Completed, SessionState.Cancelled, SessionState.Decided };

    /// <summary>Sınav süresi işliyor (süre dolunca otomatik teslim edilecek durumlar).</summary>
    public static readonly IReadOnlySet<SessionState> Running =
        new HashSet<SessionState> { SessionState.InProgress, SessionState.PermissionPending, SessionState.Disconnected };

    public static IReadOnlyCollection<(SessionState From, SessionState To)> Transitions => Allowed;

    public static bool CanTransition(SessionState from, SessionState to) => Allowed.Contains((from, to));

    /// <summary>Geçiş tabloda varsa durumu değiştirir, yoksa <see cref="InvalidSessionTransitionException"/> fırlatır.</summary>
    public static void TransitionTo(this ExamSession session, SessionState to)
    {
        if (!CanTransition(session.State, to))
        {
            throw new InvalidSessionTransitionException(session.State, to);
        }

        session.State = to;
    }
}
