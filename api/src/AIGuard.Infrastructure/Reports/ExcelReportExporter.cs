using AIGuard.Application.Abstractions;
using AIGuard.Application.Review;
using ClosedXML.Excel;

namespace AIGuard.Infrastructure.Reports;

/// <summary>Aday raporunun Excel (.xlsx) hâli (A5, FR-34). Sayfalar: Rapor, Olaylar, Notlar.</summary>
/// <remarks>
/// Not, ad ve gerekçe gibi kullanıcıdan gelen metinler hücreye düz metin olarak yazılıyor; "=" ile başlayan bir
/// not formül olarak çalışmıyor (CSV/Excel formül enjeksiyonu).
/// </remarks>
public sealed class ExcelReportExporter : IReportExporter
{
    public ReportFormat Format => ReportFormat.Xlsx;

    public ExportedReport Export(CandidateReport report, DateTimeOffset generatedAt)
    {
        using var workbook = new XLWorkbook();
        WriteSummary(workbook.AddWorksheet("Rapor"), report, generatedAt);
        WriteEvents(workbook.AddWorksheet("Olaylar"), report);
        WriteNotes(workbook.AddWorksheet("Notlar"), report);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ExportedReport(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ReportText.FileName(report, "xlsx"));
    }

    private static void WriteSummary(IXLWorksheet sheet, CandidateReport report, DateTimeOffset generatedAt)
    {
        sheet.Cell(1, 1).Value = ReportText.Title;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = $"Oluşturulma: {ReportText.Time(generatedAt)}";

        var row = 4;
        foreach (var (label, value) in ReportText.Summary(report))
        {
            Text(sheet.Cell(row, 1), label).Style.Font.Bold = true;
            Text(sheet.Cell(row, 2), value);
            row++;
        }

        row++;
        Text(sheet.Cell(row++, 1), "Öne çıkan gerekçeler").Style.Font.Bold = true;
        if (report.TopReasons.Count == 0)
        {
            Text(sheet.Cell(row++, 1), "—");
        }

        foreach (var reason in report.TopReasons)
        {
            Text(sheet.Cell(row++, 1), reason);
        }

        row++;
        if (report.MonitoringDataDeletedAt is not null)
        {
            Text(sheet.Cell(row++, 1), ReportText.MonitoringDataDeleted);
        }

        Text(sheet.Cell(row, 1), ReportText.Disclaimer).Style.Font.Italic = true;
        sheet.Column(1).Width = 34;
        sheet.Column(2).Width = 60;
        sheet.Column(2).Style.Alignment.WrapText = true;
    }

    private static void WriteEvents(IXLWorksheet sheet, CandidateReport report)
    {
        Header(sheet, "Olay türü", "Sayı", "Yanlış alarm", "Skora katkı");
        var row = 2;
        foreach (var e in report.Events)
        {
            Text(sheet.Cell(row, 1), e.Type.ToString());
            sheet.Cell(row, 2).Value = e.Count;
            sheet.Cell(row, 3).Value = e.FalseAlarms;
            sheet.Cell(row, 4).Value = e.Contribution;
            row++;
        }

        sheet.Columns(1, 4).AdjustToContents();
    }

    private static void WriteNotes(IXLWorksheet sheet, CandidateReport report)
    {
        Header(sheet, "Zaman", "Yazan", "Not");
        var row = 2;
        foreach (var note in report.Notes)
        {
            Text(sheet.Cell(row, 1), ReportText.Time(note.CreatedAt));
            Text(sheet.Cell(row, 2), note.AuthorName);
            Text(sheet.Cell(row, 3), note.Text);
            row++;
        }

        sheet.Column(1).Width = 22;
        sheet.Column(2).Width = 24;
        sheet.Column(3).Width = 80;
        sheet.Column(3).Style.Alignment.WrapText = true;
    }

    private static void Header(IXLWorksheet sheet, params string[] titles)
    {
        for (var i = 0; i < titles.Length; i++)
        {
            Text(sheet.Cell(1, i + 1), titles[i]).Style.Font.Bold = true;
        }
    }

    // Değer her zaman metin hücresi; formül olarak yorumlanmıyor.
    private static IXLCell Text(IXLCell cell, string value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = "@";
        return cell;
    }
}
