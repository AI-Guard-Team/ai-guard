using AIGuard.Application.Abstractions;
using AIGuard.Application.Review;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AIGuard.Infrastructure.Reports;

/// <summary>Aday raporunun PDF hâli (A5, FR-34). İçerik Excel'deki Rapor, Olaylar ve Notlar sayfalarıyla aynı.</summary>
/// <remarks>
/// QuestPDF Community lisansıyla kullanılıyor: akademik ve küçük ölçekli kullanımda ücretsiz; lisans türü kodda
/// bildirilmek zorunda. Yazı tipi kütüphanenin gömülü Lato'su; Türkçe karakterleri içeriyor.
/// </remarks>
public sealed class PdfReportExporter : IReportExporter
{
    static PdfReportExporter() => QuestPDF.Settings.License = LicenseType.Community;

    public ReportFormat Format => ReportFormat.Pdf;

    public ExportedReport Export(CandidateReport report, DateTimeOffset generatedAt)
    {
        var pdf = Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(t => t.FontSize(10));

            page.Header().Column(header =>
            {
                header.Item().Text(ReportText.Title).FontSize(16).Bold();
                header.Item().Text($"Oluşturulma: {ReportText.Time(generatedAt)}").FontSize(9).FontColor(Colors.Grey.Darken1);
            });

            page.Content().PaddingVertical(12).Column(body =>
            {
                body.Spacing(14);
                body.Item().Element(c => Summary(c, report));
                body.Item().Element(c => Reasons(c, report));
                body.Item().Element(c => Events(c, report));
                body.Item().Element(c => Notes(c, report));

                if (report.MonitoringDataDeletedAt is not null)
                {
                    body.Item().Text(ReportText.MonitoringDataDeleted).Italic();
                }

                body.Item().Text(ReportText.Disclaimer).FontSize(9).Italic().FontColor(Colors.Grey.Darken2);
            });

            page.Footer().AlignRight().Text(t =>
            {
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        }));

        return new ExportedReport(pdf.GeneratePdf(), "application/pdf", ReportText.FileName(report, "pdf"));
    }

    private static void Summary(IContainer container, CandidateReport report) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(170);
                c.RelativeColumn();
            });

            foreach (var (label, value) in ReportText.Summary(report))
            {
                table.Cell().Element(Row).Text(label).Bold();
                table.Cell().Element(Row).Text(value);
            }
        });

    private static void Reasons(IContainer container, CandidateReport report) =>
        container.Column(column =>
        {
            column.Item().Text("Öne çıkan gerekçeler").Bold();
            if (report.TopReasons.Count == 0)
            {
                column.Item().Text("—");
            }

            foreach (var reason in report.TopReasons)
            {
                column.Item().Text($"• {reason}");
            }
        });

    private static void Events(IContainer container, CandidateReport report) =>
        container.Column(column =>
        {
            column.Item().Text("Olay türü özeti").Bold();
            if (report.Events.Count == 0)
            {
                column.Item().Text("—");
                return;
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                });

                foreach (var title in new[] { "Olay türü", "Sayı", "Yanlış alarm", "Skora katkı" })
                {
                    table.Cell().Element(Row).Text(title).Bold();
                }

                foreach (var e in report.Events)
                {
                    table.Cell().Element(Row).Text(e.Type.ToString());
                    table.Cell().Element(Row).Text(e.Count.ToString());
                    table.Cell().Element(Row).Text(e.FalseAlarms.ToString());
                    table.Cell().Element(Row).Text(ReportText.Number(e.Contribution));
                }
            });
        });

    private static void Notes(IContainer container, CandidateReport report) =>
        container.Column(column =>
        {
            column.Item().Text("Gözetmen notları").Bold();
            if (report.Notes.Count == 0)
            {
                column.Item().Text("—");
            }

            foreach (var note in report.Notes)
            {
                column.Item().PaddingTop(4).Text($"{ReportText.Time(note.CreatedAt)} · {note.AuthorName}").FontSize(9).FontColor(Colors.Grey.Darken1);
                column.Item().Text(note.Text);
            }
        });

    private static IContainer Row(IContainer cell) =>
        cell.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingRight(6);
}
