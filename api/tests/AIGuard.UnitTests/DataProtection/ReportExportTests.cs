using System.Text;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Review;
using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Reports;
using ClosedXML.Excel;

namespace AIGuard.UnitTests.DataProtection;

/// <summary>A5 (FR-34): aday raporunun Excel ve PDF hâli.</summary>
public class ReportExportTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    // Kullanıcının yazdığı not formül gibi görünse de çalışmamalı.
    private const string FormulaNote = "=HYPERLINK(\"http://example.invalid\",\"tıkla\")";

    private static CandidateReport Report(bool started = true, DateTimeOffset? purgedAt = null) => new(
        SessionId: Guid.NewGuid(),
        ExamId: Guid.NewGuid(),
        ExamName: "Genel Yetenek Sınavı",
        ExamType: ExamType.GeneralAptitude,
        DurationMinutes: 30,
        CandidateId: Guid.NewGuid(),
        CandidateName: "Şükrü Ağaoğlu",
        CandidateEmail: "aday@example.com",
        State: SessionState.Decided,
        Status: started ? ReviewStatus.Decided : ReviewStatus.NotStarted,
        CreatedAt: Start.AddMinutes(-5),
        StartedAt: started ? Start : null,
        EndedAt: started ? Start.AddMinutes(30) : null,
        Result: started ? new ExamResultView(10, 6, 3, 1, 2) : null,
        RiskScore: 42.5,
        Flags: [SessionFlags.Risky, SessionFlags.NoAgent],
        TopReasons: ["12. dakikada 8 sn telefon görüldü"],
        Events: [new EventTypeSummary(EventType.PHONE, 2, 1, 30.25)],
        VerificationAttempts: 1,
        Decision: new ReviewDecisionView(ReviewDecisionType.Suspicious, "Sözlü görüşme önerilir", "Gözetmen Ünal", Start.AddHours(2)),
        Notes: [new ReviewNoteView(Guid.NewGuid(), FormulaNote, "Gözetmen Ünal", Start.AddHours(1))],
        MonitoringDataDeletedAt: purgedAt);

    [Fact]
    public void Excel_contains_the_report_in_turkish_and_user_text_is_never_a_formula()
    {
        var report = Report();
        var file = new ExcelReportExporter().Export(report, Start.AddHours(3));

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.Equal($"aday-raporu-{report.SessionId:N}.xlsx", file.FileName);
        Assert.DoesNotContain("Şükrü", file.FileName);

        using var workbook = new XLWorkbook(new MemoryStream(file.Content));
        Assert.Equal(["Rapor", "Olaylar", "Notlar"], workbook.Worksheets.Select(w => w.Name));

        var summary = Values(workbook.Worksheet("Rapor"));
        Assert.Contains("Şükrü Ağaoğlu", summary);
        Assert.Contains("Riskli, Ajansız", summary);
        Assert.Contains("Şüpheli (Gözetmen Ünal, 20.11.2026 15:00 (TSİ))", summary);
        Assert.Contains("12. dakikada 8 sn telefon görüldü", summary);
        Assert.Contains(summary, v => v.StartsWith("Bu rapor karar desteği içindir", StringComparison.Ordinal));

        var events = workbook.Worksheet("Olaylar");
        Assert.Equal("PHONE", events.Cell(2, 1).GetString());
        Assert.Equal(2, events.Cell(2, 2).GetDouble());

        var note = workbook.Worksheet("Notlar").Cell(2, 3);
        Assert.False(note.HasFormula);
        Assert.Equal(FormulaNote, note.GetString());
    }

    [Fact]
    public void Not_started_and_purged_sessions_say_so_instead_of_showing_zeroes()
    {
        var file = new ExcelReportExporter().Export(Report(started: false, purgedAt: Start.AddDays(31)), Start.AddDays(32));

        using var workbook = new XLWorkbook(new MemoryStream(file.Content));
        var summary = Values(workbook.Worksheet("Rapor"));
        Assert.Contains("Sınav başlamadı", summary);
        Assert.DoesNotContain("Doğru", summary);
        Assert.Contains(summary, v => v.StartsWith("Saklama süresi dolduğu için", StringComparison.Ordinal));
    }

    [Fact]
    public void Pdf_is_a_valid_document_with_a_neutral_file_name()
    {
        var report = Report(purgedAt: Start.AddDays(31));
        var file = new PdfReportExporter().Export(report, Start.AddHours(3));

        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal($"aday-raporu-{report.SessionId:N}.pdf", file.FileName);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(file.Content, 0, 5));
        Assert.True(file.Content.Length > 1000);
    }

    [Fact]
    public void Each_format_has_exactly_one_exporter()
    {
        IReportExporter[] exporters = [new ExcelReportExporter(), new PdfReportExporter()];
        Assert.Equal(Enum.GetValues<ReportFormat>(), exporters.Select(e => e.Format).Order());
    }

    private static List<string> Values(IXLWorksheet sheet) =>
        sheet.CellsUsed().Select(c => c.GetString()).ToList();
}
