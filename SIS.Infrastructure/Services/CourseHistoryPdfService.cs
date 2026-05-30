using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SIS.Application.Common.Time;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;

namespace SIS.Infrastructure.Services;

/// <summary>
/// "Alınan Dersler" PDF üreticisi. Veri akışı: ICourseRegistrationService.GetCourseHistoryReportAsync
/// → öğrenci bilgi bloğu + dönem tablosu + alt toplam.
/// </summary>
public class CourseHistoryPdfService : ICourseHistoryPdfService
{
    private readonly ICourseRegistrationService _registrationService;
    private readonly IDateTimeProvider _clock;

    private static readonly TimeZoneInfo TurkeyTimeZone = ResolveTurkeyTimeZone();

    private const string UniversityName = "Ankara Bilim Üniversitesi";
    private const string ReportTitle = "Alınan Dersler";
    private const string Watermark = "T.C.";

    public CourseHistoryPdfService(ICourseRegistrationService registrationService, IDateTimeProvider clock)
    {
        _registrationService = registrationService;
        _clock = clock;
    }

    public async Task<byte[]> GenerateAsync(int studentId, int? termId = null, CancellationToken cancellationToken = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var report = await _registrationService.GetCourseHistoryReportAsync(studentId, termId)
            ?? throw new InvalidOperationException($"Öğrenci bulunamadı: {studentId}");

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, TurkeyTimeZone);
        var generatedAt = localNow.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9));

                // ── Üst başlık: T.C. / Üniversite / Rapor adı + sağda tarih ──
                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(Watermark).FontSize(9);
                    col.Item().AlignCenter().Text(UniversityName).FontSize(11).Bold();
                    col.Item().AlignCenter().Text(ReportTitle).FontSize(10).SemiBold();
                    col.Item().PaddingTop(2).Row(row =>
                    {
                        row.RelativeItem();
                        row.ConstantItem(120).AlignRight().Text(generatedAt).FontSize(8);
                    });
                    col.Item().PaddingTop(4).LineHorizontal(0.8f);
                });

                page.Content().PaddingVertical(6).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Element(c => RenderStudentInfoBlock(c, report));
                    col.Item().Element(c => RenderCoursesTable(c, report));
                    col.Item().Element(c => RenderTotalsRow(c, report));
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.CurrentPageNumber().FontSize(8);
                    t.Span(" / ").FontSize(8);
                    t.TotalPages().FontSize(8);
                });
            });
        });

        return document.GeneratePdf();
    }

    // ────────────────────────────────────────────────────────────────────
    private static void RenderStudentInfoBlock(IContainer container, CourseHistoryReportDto r)
    {
        container.Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(8).Column(col =>
        {
            col.Spacing(3);
            RenderInfoLine(col, "Öğrenci No",  r.StudentNumber);
            RenderInfoLine(col, "Adı Soyadı",  r.FullName);
            RenderInfoLine(col, "Sınıfı",      r.CurrentClassYear?.ToString(CultureInfo.InvariantCulture) ?? "—");
            RenderInfoLine(col, "Fakülte",     r.FacultyName ?? "—");
            RenderInfoLine(col, "Program",     r.ProgramName ?? "—");
            RenderInfoLine(col, "Dönemi",      r.ActiveTermName ?? "—");
            RenderInfoLine(col, "Danışmanı",   r.AdvisorName ?? "—");
        });
    }

    private static void RenderInfoLine(ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(90).Text(label).Bold().FontSize(9);
            row.ConstantItem(10).Text(":").FontSize(9);
            row.RelativeItem().Text(value).FontSize(9);
        });
    }

    // ────────────────────────────────────────────────────────────────────
    private static void RenderCoursesTable(IContainer container, CourseHistoryReportDto r)
    {
        var allCourses = r.Terms.SelectMany(t => t.Courses).ToList();
        var headerColor = Colors.Grey.Lighten2;
        var rowBorder   = Colors.Grey.Lighten1;

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(28);   // Şube
                c.ConstantColumn(58);   // Ders Kodu
                c.RelativeColumn(3);    // Adı
                c.ConstantColumn(24);   // Teo.
                c.ConstantColumn(24);   // Lab.
                c.ConstantColumn(24);   // Uyg.
                c.ConstantColumn(32);   // Kredi
                c.ConstantColumn(32);   // AKTS
                c.ConstantColumn(40);   // Harf Notu
                c.ConstantColumn(70);   // Alış Şekli
                c.RelativeColumn(2);    // Öğretim Görevlisi
            });

            // Header
            table.Header(h =>
            {
                foreach (var label in new[]
                {
                    "Şube", "Ders Kodu", "Adı", "Teo.", "Lab.", "Uyg.",
                    "Kredi", "AKTS", "Harf Notu", "Alış Şekli", "Öğretim Görevlisi"
                })
                {
                    h.Cell().Background(headerColor).BorderBottom(0.5f).BorderColor(Colors.Grey.Medium)
                        .Padding(3).AlignCenter().AlignMiddle()
                        .Text(label).FontSize(8).Bold();
                }
            });

            // Body — local functions capture `table` from outer lambda closure
            void AddCenter(string text) =>
                table.Cell().BorderBottom(0.3f).BorderColor(rowBorder).Padding(3)
                    .AlignCenter().AlignMiddle().Text(text).FontSize(8);

            void AddLeft(string text) =>
                table.Cell().BorderBottom(0.3f).BorderColor(rowBorder).Padding(3)
                    .AlignLeft().AlignMiddle().Text(text).FontSize(8);

            foreach (var c in allCourses)
            {
                AddCenter(c.SectionNumber.ToString(CultureInfo.InvariantCulture));
                AddCenter(c.CourseCode);
                AddLeft  (c.CourseName);
                AddCenter(c.TheoryHours.ToString(CultureInfo.InvariantCulture));
                AddCenter(c.LabHours.ToString(CultureInfo.InvariantCulture));
                AddCenter((c.ApplicationHours ?? 0).ToString(CultureInfo.InvariantCulture));
                AddCenter(c.NationalCredit.ToString(CultureInfo.InvariantCulture));
                AddCenter(c.Ects.ToString(CultureInfo.InvariantCulture));
                AddCenter(c.Grade ?? "—");
                AddLeft  (c.AcquisitionType);
                AddLeft  (c.InstructorName);
            }
        });
    }

    // ────────────────────────────────────────────────────────────────────
    private static void RenderTotalsRow(IContainer container, CourseHistoryReportDto r)
    {
        var line = $"Toplam Ders Sayısı: {r.TotalCourses}   Ders Saati: {r.TotalTheoryHours}   Kredi: {r.TotalCredit}   AKTS: {r.TotalEcts}";
        container.PaddingTop(6).Text(line).Bold().FontSize(9);
    }

    private static TimeZoneInfo ResolveTurkeyTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Local; }
    }
}
