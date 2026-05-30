using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SIS.Infrastructure.Services.Scheduling.Reports;

/// <summary>
/// Öğrenci + öğretim elemanı ders programı için ortak PDF renderer (A4 landscape, gün-bazlı tablolar).
/// Tek kaynak — <see cref="TimetablePdfData"/> alır.
/// </summary>
public sealed class TimetablePdfRenderer
{
    private static readonly TimeZoneInfo TurkeyTimeZone = ResolveTimeZone();

    // Gün sırası: Pzt–Cuma her zaman aday; Cmt (6) ve Paz (0) yalnızca o güne ders varsa basılır.
    private static readonly int[] DayOrder = { 1, 2, 3, 4, 5, 6, 0 };

    public byte[] Render(TimetablePdfData data)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(15);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(8).FontFamily("Arial"));

                page.Header().Element(header =>
                {
                    header.Column(col =>
                    {
                        col.Item().AlignRight().Text(BuildTimestamp())
                            .FontSize(7).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(2)
                            .Background(Colors.Grey.Lighten3)
                            .Padding(4)
                            .AlignCenter()
                            .Text($"{data.Title} / {data.SubjectName} - {data.TermName}")
                            .Bold().FontSize(10);
                    });
                });

                page.Content().PaddingVertical(8).Row(row =>
                {
                    var anyRendered = false;
                    foreach (var day in DayOrder)
                    {
                        var dayRows = ExtractDayRows(data, day);
                        if (dayRows.Count == 0) continue;
                        anyRendered = true;
                        row.RelativeItem().Padding(3).Element(c => RenderDayTable(c, day, dayRows));
                    }
                    if (!anyRendered)
                    {
                        row.RelativeItem().Padding(20).AlignCenter()
                            .Text("Tanımlı Ders Programı Bulunamadı!")
                            .FontSize(11).Italic().FontColor(Colors.Grey.Darken1);
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static List<DayRow> ExtractDayRows(TimetablePdfData data, int day)
    {
        var rows = new List<DayRow>();
        foreach (var section in data.Sections)
        {
            foreach (var block in section.Blocks.Where(b => b.Day == day))
            {
                rows.Add(new DayRow(
                    SectionNumber: section.SectionNumber,
                    CourseCode: section.CourseCode,
                    ClassroomLabel: block.ClassroomName ?? "Çevrimiçi",
                    Start: block.StartTime.ToString("HH:mm"),
                    End: block.EndTime.ToString("HH:mm"),
                    IsLab: block.IsLab));
            }
        }
        return rows.OrderBy(r => r.Start).ToList();
    }

    private static void RenderDayTable(IContainer container, int day, List<DayRow> rows)
    {
        container.Column(col =>
        {
            col.Item().Background(Colors.Grey.Lighten3).Padding(2).AlignCenter()
                .Text(DayName(day)).Bold().FontSize(9);
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1);
                    c.RelativeColumn(2.5f);
                    c.RelativeColumn(2.5f);
                    c.RelativeColumn(1.5f);
                    c.RelativeColumn(1.5f);
                });

                table.Header(h =>
                {
                    h.Cell().Padding(2).Text("Şb.").Bold();
                    h.Cell().Padding(2).Text("Ders Kodu").Bold();
                    h.Cell().Padding(2).Text("Derslik Kodu").Bold();
                    h.Cell().Padding(2).Text("Baş.Saat").Bold();
                    h.Cell().Padding(2).Text("Bit.Saat").Bold();
                });

                foreach (var row in rows)
                {
                    var bg = row.IsLab ? "#E8F5E9" : "#FFFFFF";
                    table.Cell().Background(bg).Padding(2).Text(row.SectionNumber);
                    table.Cell().Background(bg).Padding(2).Text(row.CourseCode);
                    table.Cell().Background(bg).Padding(2).Text(row.ClassroomLabel);
                    table.Cell().Background(bg).Padding(2).Text(row.Start);
                    table.Cell().Background(bg).Padding(2).Text(row.End);
                }
            });
        });
    }

    private static string DayName(int day) => day switch
    {
        1 => "Pazartesi",
        2 => "Salı",
        3 => "Çarşamba",
        4 => "Perşembe",
        5 => "Cuma",
        6 => "Cumartesi",
        0 => "Pazar",
        _ => string.Empty,
    };

    private static string BuildTimestamp()
    {
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TurkeyTimeZone);
        return now.ToString("dd.MM.yyyy HH:mm");
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }
        catch
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
            catch { return TimeZoneInfo.Utc; }
        }
    }

    private sealed record DayRow(
        string SectionNumber,
        string CourseCode,
        string ClassroomLabel,
        string Start,
        string End,
        bool IsLab);
}
