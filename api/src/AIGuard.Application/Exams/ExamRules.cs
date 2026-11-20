using AIGuard.Domain.Entities;

namespace AIGuard.Application.Exams;

public enum StartDenial
{
    /// <summary>Aday bu sınava atanmamış.</summary>
    NotAssigned,

    /// <summary>Erişim penceresi henüz açılmadı.</summary>
    WindowNotOpen,

    /// <summary>Son başlangıç zamanı geçti (pencere bitişi − süre).</summary>
    TooLate,

    /// <summary>Tek oturum hakkı kullanılmış.</summary>
    AttemptUsed,
}

/// <summary>
/// Erişim penceresi ve tek oturum hakkı kuralları (E3, FR-06). Veritabanına dokunmuyor; E4'teki
/// oturum başlatma ucu bu kuralları çağırıyor, böylece kural tek yerde ve veritabanısız test ediliyor.
/// </summary>
public static class ExamRules
{
    /// <summary>
    /// Son başlangıç zamanı = pencere bitişi − sınav süresi. Bu andan sonra başlayan aday süresini
    /// pencere içinde dolduramaz.
    /// </summary>
    public static DateTimeOffset LatestStartAt(Exam exam) => exam.WindowEndAt.AddMinutes(-exam.DurationMinutes);

    /// <summary>Adayın şu an sınavı başlatıp başlatamayacağı; başlatabiliyorsa null.</summary>
    /// <param name="assignment">Adayın bu sınavdaki ataması; yoksa null.</param>
    public static StartDenial? CanStart(Exam exam, ExamAssignment? assignment, DateTimeOffset now)
    {
        if (assignment is null || assignment.ExamId != exam.Id || assignment.Role != Domain.Enums.AssignmentRole.Candidate)
        {
            return StartDenial.NotAssigned;
        }

        // Hak, oturum gerçekten başladığında doluyor; rıza reddedilip iptal edilen oturum saymıyor (FR-10).
        if (assignment.AttemptUsedAt is not null)
        {
            return StartDenial.AttemptUsed;
        }

        if (now < exam.WindowStartAt)
        {
            return StartDenial.WindowNotOpen;
        }

        return now > LatestStartAt(exam) ? StartDenial.TooLate : null;
    }

    /// <summary>Sınav alanlarının kuralları; geçerliyse null, değilse Türkçe mesaj.</summary>
    public static string? Validate(ExamRequest r)
    {
        var name = r.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 200)
        {
            return "Sınav adı 1–200 karakter olmalıdır.";
        }

        if (r.Type is not { } type || !Enum.IsDefined(type))
        {
            return "Sınav türü GeneralAptitude veya EnglishProficiency olmalıdır.";
        }

        // Üst sınır: yanlışlıkla girilen çok büyük değerler pencere hesabını taşırmasın.
        if (r.DurationMinutes is not (> 0 and <= 600))
        {
            return "Sınav süresi 1–600 dakika olmalıdır.";
        }

        if (r.WindowStartAt is not { } start || r.WindowEndAt is not { } end)
        {
            return "Erişim penceresinin başlangıç ve bitiş zamanı zorunludur.";
        }

        if (end <= start)
        {
            return "Erişim penceresinin bitişi başlangıcından sonra olmalıdır.";
        }

        // Pencere sınav süresinden kısaysa son başlangıç zamanı başlangıçtan önce kalır; kimse giremez.
        if (end.AddMinutes(-r.DurationMinutes.Value) < start)
        {
            return "Erişim penceresi sınav süresinden kısa olamaz.";
        }

        if (r.RetentionDays is { } retention && retention is not (> 0 and <= 3650))
        {
            return "Saklama süresi 1–3650 gün olmalıdır.";
        }

        return null;
    }

    public const int MaxOptions = 10;
    public const int MaxQuestionLength = 2000;
    public const int MaxOptionLength = 500;

    /// <summary>Soru kuralları (çoktan seçmeli): en az 2 seçenek, doğru seçenek aralıkta. Geçerliyse null.</summary>
    public static string? Validate(QuestionRequest r)
    {
        var text = r.Text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > MaxQuestionLength)
        {
            return $"Soru metni 1–{MaxQuestionLength} karakter olmalıdır.";
        }

        if (r.Options is not { Count: >= 2 and <= MaxOptions } options)
        {
            return $"Soruda 2–{MaxOptions} seçenek olmalıdır.";
        }

        if (options.Any(o => string.IsNullOrWhiteSpace(o) || o.Trim().Length > MaxOptionLength))
        {
            return $"Seçenekler boş olamaz ve en fazla {MaxOptionLength} karakter olabilir.";
        }

        if (r.CorrectOptionIndex is not { } correct || correct < 0 || correct >= options.Count)
        {
            return "Doğru seçenek, seçeneklerden biri olmalıdır.";
        }

        if (r.Order is < 0)
        {
            return "Sıra negatif olamaz.";
        }

        return null;
    }
}
