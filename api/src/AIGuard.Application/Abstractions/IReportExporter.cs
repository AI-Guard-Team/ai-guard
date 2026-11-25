using AIGuard.Application.Review;

namespace AIGuard.Application.Abstractions;

/// <summary>Raporun indirilebilir biçimleri (FR-34).</summary>
public enum ReportFormat
{
    Xlsx,
    Pdf,
}

/// <param name="FileName">Kişisel veri içermiyor (aday adı yok); oturum kimliğinden üretiliyor.</param>
public sealed record ExportedReport(byte[] Content, string ContentType, string FileName);

/// <summary>
/// Aday raporunu dosyaya çevirir (A5, FR-34). Raporun içeriği <see cref="CandidateReport"/> ile aynı; dosya
/// kütüphaneleri Infrastructure'da (K-14). Her biçim için bir uygulama var.
/// </summary>
public interface IReportExporter
{
    ReportFormat Format { get; }

    /// <param name="generatedAt">Dosyaya "oluşturulma" olarak yazılan an.</param>
    ExportedReport Export(CandidateReport report, DateTimeOffset generatedAt);
}
