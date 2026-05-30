using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.Reports;

/// <summary>
/// Bölüm Ders Programı PDF renderer — Proliz tarzı 2x2 grid layout.
/// </summary>
public sealed class DepartmentSchedulePdfRenderer
{
    private static readonly TimeZoneInfo TurkeyTimeZone = ResolveTimeZone();

    public byte[] Render(DepartmentScheduleDto data)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(15);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(7).FontFamily("Arial"));

                page.Header().Element(header =>
                {
                    header.Column(col =>
                    {
                        col.Item().AlignRight().Text(BuildTimestamp())
                            .FontSize(7).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(2)
                            .Background(Colors.Grey.Lighten3)
                            .Padding(4)
                            .Column(headerCol =>
                            {
                                headerCol.Item().AlignCenter().Text("Bölüm Ders Programı")
                                    .Bold().FontSize(11);
                                headerCol.Item().AlignCenter().Text($"{data.ProgramName} — {data.ClassYear}. Sınıf — {data.TermName}")
                                    .FontSize(9);
                            });
                    });
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    // 2x2 grid: row 1 = (Pzt, Sal), row 2 = (Çar, Per), row 3 = (Cum, boş)
                    var pairs = new[]
                    {
                        (1, 2),
                        (3, 4),
                        (5, 0),
                    };

                    foreach (var (leftDay, rightDay) in pairs)
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Padding(3).Element(c => RenderDay(c, leftDay, ExtractDayRows(data, leftDay)));
                            if (rightDay > 0)
                                row.RelativeItem().Padding(3).Element(c => RenderDay(c, rightDay, ExtractDayRows(data, rightDay)));
                            else
                                row.RelativeItem().Padding(3);
                        });
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

    private static List<DayRow> ExtractDayRows(DepartmentScheduleDto data, int day)
    {
        var rows = new List<DayRow>();
        foreach (var section in data.Sections)
        {
            foreach (var block in section.Blocks.Where(b => b.Day == day))
            {
                rows.Add(new DayRow(
                    SectionNumber: section.SectionNumber,
                    CourseCode: section.CourseCode,
                    CourseName: section.CourseName,
                    ClassroomLabel: FormatClassroom(block),
                    InstructorDisplay: section.InstructorDisplay,
                    Start: block.StartTime.ToString("HH:mm"),
                    End: block.EndTime.ToString("HH:mm"),
                    IsLab: block.BlockType == ScheduleBlockType.Lab));
            }
        }
        return rows.OrderBy(r => r.Start).ToList();
    }

    private static void RenderDay(IContainer container, int day, List<DayRow> rows)
    {
        container.Column(col =>
        {
            col.Item().Background(Colors.Grey.Lighten3).Padding(3).Text(DayName(day)).Bold().FontSize(9);

            if (rows.Count == 0)
            {
                col.Item().Padding(6).Text("Tanımlı Ders Programı Bulunamadı!")
                    .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                return;
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.4f); // Saat
                    c.RelativeColumn(1.4f); // Ders Kodu
                    c.RelativeColumn(3.0f); // Ders Adı
                    c.RelativeColumn(1.2f); // Derslik
                    c.RelativeColumn(2.0f); // Öğretim Elemanı
                });

                table.Header(h =>
                {
                    h.Cell().Padding(2).Text("Saat").Bold();
                    h.Cell().Padding(2).Text("Ders Kodu").Bold();
                    h.Cell().Padding(2).Text("Ders Adı").Bold();
                    h.Cell().Padding(2).Text("Derslik").Bold();
                    h.Cell().Padding(2).Text("Öğretim Elemanı").Bold();
                });

                foreach (var row in rows)
                {
                    var bg = row.IsLab ? "#E8F5E9" : "#FFFFFF";
                    table.Cell().Background(bg).Padding(2).Text($"{row.Start}-{row.End}");
                    table.Cell().Background(bg).Padding(2).Text($"{row.CourseCode}({row.SectionNumber})");
                    table.Cell().Background(bg).Padding(2).Text(row.CourseName);
                    table.Cell().Background(bg).Padding(2).Text(row.ClassroomLabel);
                    table.Cell().Background(bg).Padding(2).Text(row.InstructorDisplay);
                }
            });
        });
    }

    private static string FormatClassroom(DepartmentScheduleBlockDto block)
    {
        if (string.IsNullOrWhiteSpace(block.ClassroomName)) return "Çevrimiçi";
        return block.ClassroomCapacity.HasValue
            ? $"{block.ClassroomName}[{block.ClassroomCapacity.Value}]"
            : block.ClassroomName;
    }

    private static string DayName(int day) => day switch
    {
        1 => "Pazartesi",
        2 => "Salı",
        3 => "Çarşamba",
        4 => "Perşembe",
        5 => "Cuma",
        6 => "Cumartesi",
        7 => "Pazar",
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
        string CourseName,
        string ClassroomLabel,
        string InstructorDisplay,
        string Start,
        string End,
        bool IsLab);
}
