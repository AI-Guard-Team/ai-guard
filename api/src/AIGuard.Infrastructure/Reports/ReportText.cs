using System.Globalization;
using AIGuard.Application.Review;
using AIGuard.Domain.Enums;

namespace AIGuard.Infrastructure.Reports;

/// <summary>Excel ve PDF raporunun ortak Türkçe etiketleri; iki dosya aynı dili konuşsun diye tek yerde.</summary>
internal static class ReportText
{
    public const string Title = "Aday Sınav Raporu";

    // GAB 3.3.8: sistem karar vermez, karar desteği sunar. Rapor dışarı çıktığında da bu görünmeli.
    public const string Disclaimer =
        "Bu rapor karar desteği içindir. Sistem kopya kararı vermez; olaylar ve risk skoru gözetmenin değerlendirmesine " +
        "sunulur, nihai karar gözetmene aittir.";

    public const string MonitoringDataDeleted =
        "Saklama süresi dolduğu için olay kayıtları ve kanıt kareleri silindi; skor, sonuç ve karar kayıtlı hâliyle duruyor.";

    // Türkiye 2016'dan beri yaz saati uygulamıyor; sabit UTC+3.
    private static readonly TimeSpan Turkey = TimeSpan.FromHours(3);

    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string Time(DateTimeOffset? value) =>
        value is { } v ? v.ToOffset(Turkey).ToString("dd.MM.yyyy HH:mm", Tr) + " (TSİ)" : "—";

    public static string Number(double value) => value.ToString("0.##", Tr);

    public static string Status(ReviewStatus status) => status switch
    {
        ReviewStatus.NotStarted => "Başlamadı",
        ReviewStatus.AwaitingReview => "Karar bekliyor",
        ReviewStatus.Completed => "Tamamlandı (işaret yok)",
        ReviewStatus.Decided => "Karar verildi",
        _ => status.ToString(),
    };

    public static string Decision(ReviewDecisionType decision) => decision switch
    {
        ReviewDecisionType.Clean => "Temiz",
        ReviewDecisionType.Suspicious => "Şüpheli",
        ReviewDecisionType.Violation => "İhlal",
        _ => decision.ToString(),
    };

    public static string ExamType(ExamType type) => type switch
    {
        Domain.Enums.ExamType.GeneralAptitude => "Genel yetenek",
        Domain.Enums.ExamType.EnglishProficiency => "İngilizce yeterlik",
        _ => type.ToString(),
    };

    public static string Flags(IReadOnlyList<SessionFlags> flags) =>
        flags.Count == 0 ? "Yok" : string.Join(", ", flags.Select(Flag));

    private static string Flag(SessionFlags flag) => flag switch
    {
        SessionFlags.Risky => "Riskli",
        SessionFlags.Indicated => "Ajan göstergesi",
        SessionFlags.NoAgent => "Ajansız",
        SessionFlags.MonitoringGap => "İzleme kesintisi",
        SessionFlags.PermissionGap => "İzinsiz süre",
        _ => flag.ToString(),
    };

    /// <summary>Raporun "alan: değer" satırları; iki biçim de aynı sırayla yazıyor.</summary>
    public static IReadOnlyList<(string Label, string Value)> Summary(CandidateReport r)
    {
        var rows = new List<(string, string)>
        {
            ("Sınav", r.ExamName),
            ("Sınav türü", ExamType(r.ExamType)),
            ("Süre", $"{r.DurationMinutes} dakika"),
            ("Aday", r.CandidateName),
            ("E-posta", r.CandidateEmail),
            ("İnceleme durumu", Status(r.Status)),
            ("Başlangıç", Time(r.StartedAt)),
            ("Bitiş", Time(r.EndedAt)),
        };

        if (r.Result is { } result)
        {
            rows.Add(("Soru sayısı", result.QuestionCount.ToString(Tr)));
            rows.Add(("Doğru", result.Correct.ToString(Tr)));
            rows.Add(("Yanlış", result.Wrong.ToString(Tr)));
            rows.Add(("Boş", result.Blank.ToString(Tr)));
            rows.Add(("Bağlantı kesikken verilen cevap", result.Unmonitored.ToString(Tr)));
        }
        else
        {
            rows.Add(("Sınav sonucu", "Sınav başlamadı"));
        }

        rows.Add(("Risk skoru", Number(r.RiskScore)));
        rows.Add(("İşaretler", Flags(r.Flags)));
        rows.Add(("Doğrulama denemesi", r.VerificationAttempts.ToString(Tr)));
        rows.Add(("Karar", r.Decision is { } d ? $"{Decision(d.Decision)} ({d.ReviewerName}, {Time(d.DecidedAt)})" : "Verilmedi"));
        if (r.Decision?.Note is { } note)
        {
            rows.Add(("Karar notu", note));
        }

        return rows;
    }

    public static string FileName(CandidateReport r, string extension) => $"aday-raporu-{r.SessionId:N}.{extension}";
}
