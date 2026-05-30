using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SIS.Application.Common.Time;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

/// <summary>
/// "Seçilen Dersler" PDF üreticisi.
/// Öğrencinin aktif dönemde seçtiği (Draft/PendingAdvisorApproval/Active/Rejected) kayıtları
/// resmî PDF olarak üretir. Dropped ve Withdrawn kayıtlar dahil edilmez.
/// Görsel stil CourseHistoryPdfService ile tutarlıdır.
/// </summary>
public sealed class RegistrationPlanPdfService : IRegistrationPlanPdfService
{
    private readonly AppDbContext     _context;
    private readonly IDateTimeProvider _clock;

    private static readonly TimeZoneInfo TurkeyTimeZone = ResolveTurkeyTimeZone();

    private const string UniversityName = "Ankara Bilim Üniversitesi";
    private const string ReportTitle    = "Seçilen Dersler";
    private const string Watermark      = "T.C.";

    public RegistrationPlanPdfService(AppDbContext context, IDateTimeProvider clock)
    {
        _context = context;
        _clock   = clock;
    }

    public async Task<byte[]> GenerateAsync(int studentId, CancellationToken cancellationToken = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        // ── Öğrenci bilgisi ────────────────────────────────────────────────
        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.Faculty)
            .Include(s => s.Advisor)
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken)
            ?? throw new InvalidOperationException($"Öğrenci bulunamadı: {studentId}");

        var activeProgram = student.StudentPrograms.FirstOrDefault(sp => sp.IsActive);
        var mainProgram   = student.StudentPrograms.FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive)
                         ?? activeProgram;

        // Zorunlu/Seçmeli haritası (müfredattan)
        var compulsoryMap = mainProgram != null
            ? await _context.CurriculumCourses
                .AsNoTracking()
                .Where(cc => cc.CurriculumId == mainProgram.CurriculumId)
                .ToDictionaryAsync(cc => cc.CourseId, cc => cc.IsCompulsory, cancellationToken)
            : new Dictionary<int, bool>();

        // Öğrenci kullanıcı adı
        var studentUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == student.UserId, cancellationToken);
        var fullName = studentUser == null
            ? string.Empty
            : $"{studentUser.FirstName} {studentUser.LastName}".Trim();

        // Danışman adı
        string? advisorName = null;
        if (student.Advisor != null)
        {
            var advisorUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == student.Advisor.UserId, cancellationToken);
            if (advisorUser != null)
                advisorName = $"{student.Advisor.Title} {advisorUser.FirstName} {advisorUser.LastName}".Trim();
        }

        // ── Aktif dönem ────────────────────────────────────────────────────
        var activeTerm = await _context.AcademicTerms
            .AsNoTracking()
            .ActiveTerm()
            .FirstOrDefaultAsync(cancellationToken);

        if (activeTerm == null)
            throw new InvalidOperationException("Aktif dönem bulunamadı.");

        // ── Seçilen dersler (Dropped/Withdrawn hariç) ──────────────────────
        var registrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Instructor)
            .Where(cr =>
                cr.StudentId == studentId
                && cr.CourseSection.TermId == activeTerm.Id
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Withdrawn
                && cr.Status != RegistrationStatus.Completed)
            .OrderBy(cr => cr.CourseSection.Course.Code)
            .ToListAsync(cancellationToken);

        // Instructor kullanıcı adları
        var instructorUserIds = registrations
            .Where(r => r.CourseSection.Instructor != null)
            .Select(r => r.CourseSection.Instructor!.UserId)
            .Distinct()
            .ToList();

        var instructorNames = instructorUserIds.Any()
            ? await _context.Users
                .AsNoTracking()
                .Where(u => instructorUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), cancellationToken)
            : new Dictionary<int, string>();

        // ── PDF üretimi ────────────────────────────────────────────────────
        var localNow     = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, TurkeyTimeZone);
        var generatedAt  = localNow.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

        var headerInfo = new HeaderInfo(
            StudentNumber: student.StudentNumber,
            FullName:      fullName,
            ClassYear:     (student.CompletedSemesters / 2) + 1,
            FacultyName:   activeProgram?.Program?.Department?.Faculty?.Name ?? "—",
            ProgramName:   activeProgram?.Program?.Name ?? "—",
            TermName:      activeTerm.Name,
            AdvisorName:   advisorName ?? "—"
        );

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9));

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
                    col.Item().Element(c => RenderStudentInfoBlock(c, headerInfo));
                    col.Item().Element(c => RenderCoursesTable(c, registrations, instructorNames, compulsoryMap));
                    col.Item().Element(c => RenderTotalsRow(c, registrations));
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

    // ── Öğrenci bilgi bloğu ────────────────────────────────────────────────
    private static void RenderStudentInfoBlock(IContainer container, HeaderInfo h)
    {
        container.Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(8).Column(col =>
        {
            col.Spacing(3);
            RenderInfoLine(col, "Öğrenci No", h.StudentNumber);
            RenderInfoLine(col, "Adı Soyadı", h.FullName);
            RenderInfoLine(col, "Sınıfı",     h.ClassYear.ToString(CultureInfo.InvariantCulture));
            RenderInfoLine(col, "Fakülte",    h.FacultyName);
            RenderInfoLine(col, "Program",    h.ProgramName);
            RenderInfoLine(col, "Dönemi",     h.TermName);
            RenderInfoLine(col, "Danışmanı",  h.AdvisorName);
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

    // ── Ders tablosu ──────────────────────────────────────────────────────
    private static void RenderCoursesTable(
        IContainer container,
        List<Domain.Entities.CourseRegistration> registrations,
        Dictionary<int, string> instructorNames,
        Dictionary<int, bool> compulsoryMap)
    {
        var headerColor = Colors.Grey.Lighten2;
        var rowBorder   = Colors.Grey.Lighten1;

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(50);   // Ders Kodu
                c.RelativeColumn(3);    // Adı
                c.ConstantColumn(28);   // Z/S
                c.ConstantColumn(28);   // T+U
                c.ConstantColumn(32);   // Kredi
                c.ConstantColumn(32);   // AKTS
                c.ConstantColumn(70);   // Durum
            });

            table.Header(h =>
            {
                foreach (var label in new[] { "Ders Kodu", "Adı", "Z/S", "T+U", "Kredi", "AKTS", "Durum" })
                {
                    h.Cell().Background(headerColor).BorderBottom(0.5f).BorderColor(Colors.Grey.Medium)
                        .Padding(3).AlignCenter().AlignMiddle()
                        .Text(label).FontSize(8).Bold();
                }
            });

            void AddCenter(string text) =>
                table.Cell().BorderBottom(0.3f).BorderColor(rowBorder).Padding(3)
                    .AlignCenter().AlignMiddle().Text(text).FontSize(8);

            void AddLeft(string text) =>
                table.Cell().BorderBottom(0.3f).BorderColor(rowBorder).Padding(3)
                    .AlignLeft().AlignMiddle().Text(text).FontSize(8);

            foreach (var reg in registrations)
            {
                var course  = reg.CourseSection.Course;
                var ects    = reg.EctsSnapshot   ?? course.Ects;
                var credit  = reg.CreditSnapshot ?? course.NationalCredit;
                var hours   = course.TheoryHours + course.LabHours;
                var zs      = compulsoryMap.TryGetValue(course.Id, out var isComp) && isComp ? "Z" : "S";

                AddCenter(course.Code);
                AddLeft  (course.Name);
                AddCenter(zs);
                AddCenter(hours.ToString(CultureInfo.InvariantCulture));
                AddCenter(credit.ToString(CultureInfo.InvariantCulture));
                AddCenter(ects.ToString(CultureInfo.InvariantCulture));
                AddCenter(TranslateStatus(reg.Status));
            }
        });
    }

    // ── Alt toplam satırı ─────────────────────────────────────────────────
    private static void RenderTotalsRow(
        IContainer container,
        List<Domain.Entities.CourseRegistration> registrations)
    {
        var totalCourses = registrations.Count;
        var totalHours   = registrations.Sum(r =>
            r.CourseSection.Course.TheoryHours + r.CourseSection.Course.LabHours);
        var totalCredit  = registrations.Sum(r =>
            r.CreditSnapshot ?? r.CourseSection.Course.NationalCredit);
        var totalEcts    = registrations.Sum(r =>
            r.EctsSnapshot ?? r.CourseSection.Course.Ects);

        var line = $"Toplam Ders Sayısı: {totalCourses}   Ders Saati: {totalHours}   Kredi: {totalCredit}   AKTS: {totalEcts}";
        container.PaddingTop(6).Text(line).Bold().FontSize(9);
    }

    // ── Yardımcı ──────────────────────────────────────────────────────────
    private static string TranslateStatus(RegistrationStatus status) => status switch
    {
        RegistrationStatus.Draft                  => "Taslak",
        RegistrationStatus.PendingAdvisorApproval => "Danışman Onayı Bekliyor",
        RegistrationStatus.Active                 => "Onaylandı",
        RegistrationStatus.Rejected               => "Reddedildi",
        _                                         => status.ToString()
    };

    private static TimeZoneInfo ResolveTurkeyTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
        catch (InvalidTimeZoneException)  { return TimeZoneInfo.Local; }
    }

    // ── İç kayıt türü ─────────────────────────────────────────────────────
    private sealed record HeaderInfo(
        string StudentNumber,
        string FullName,
        int    ClassYear,
        string FacultyName,
        string ProgramName,
        string TermName,
        string AdvisorName);
}
