using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SIS.Application.Reports;

namespace SIS.Infrastructure.Services.Reports;

public sealed class ReportExportService : IReportExportService
{
    public byte[] ToXlsx(string title, ReportResult result)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Rapor");

        for (var c = 0; c < result.Columns.Count; c++)
            ws.Cell(1, c + 1).Value = result.Columns[c].Label;
        ws.Row(1).Style.Font.Bold = true;

        for (var r = 0; r < result.Rows.Count; r++)
        {
            var row = result.Rows[r];
            for (var c = 0; c < row.Count; c++)
                ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(row[c]);
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] ToPdf(string title, ReportResult result)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Text(title).FontSize(16).Bold();

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        for (var i = 0; i < result.Columns.Count; i++) cols.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var col in result.Columns)
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(col.Label).Bold();
                    });

                    foreach (var row in result.Rows)
                        for (var c = 0; c < result.Columns.Count; c++)
                        {
                            var value = c < row.Count ? row[c]?.ToString() ?? string.Empty : string.Empty;
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(value);
                        }
                });

                page.Footer().AlignRight().Text(x =>
                {
                    x.Span($"Toplam {result.TotalCount} kayıt · ");
                    x.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                });
            });
        });

        return document.GeneratePdf();
    }
}
