using Microsoft.EntityFrameworkCore;
using SIS.Application.Reports;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Reports.Handlers;

/// <summary>
/// "Ders Kayıt Listesi" — bir dönem/şubedeki ders kayıtları.
/// Parametreler: termId (Term, ops.), courseSectionId (int, ops.), status (RegistrationStatus, ops.).
/// Scope aktifse öğrencinin fakültesine göre filtreler.
/// </summary>
public sealed class CourseRegistrationReportHandler : IReportHandler
{
    private readonly AppDbContext _db;
    public CourseRegistrationReportHandler(AppDbContext db) => _db = db;

    public string Code => "ders-kayit-listesi";

    public ReportParameterSchema GetParameterSchema() => new(new ReportParameter[]
    {
        new("termId", "Dönem", ReportParameterType.Term, Required: false),
        new("courseSectionId", "Şube", ReportParameterType.CourseSection, Required: false),
        new("status", "Kayıt Durumu", ReportParameterType.Enum, Required: false,
            Options: Enum.GetNames<RegistrationStatus>().Select(n => new ReportParameterOption(n, n)).ToList()),
    });

    public async Task<ReportResult> RunAsync(ReportRunRequest request, ReportExecutionContext ctx, CancellationToken ct)
    {
        var p = request.Parameters;
        IQueryable<Domain.Entities.CourseRegistration> q = _db.Set<Domain.Entities.CourseRegistration>().AsNoTracking();

        if (p.TryGetInt("termId", out var termId))
            q = q.Where(r => r.CourseSection.TermId == termId);
        if (p.TryGetInt("courseSectionId", out var sectionId))
            q = q.Where(r => r.CourseSectionId == sectionId);
        if (p.TryGetEnum("status", out RegistrationStatus status))
            q = q.Where(r => r.Status == status);

        if (!ctx.IsSystemAdmin && ctx.ScopeActive && ctx.ScopeFacultyIds.Count > 0)
        {
            var facultyIds = ctx.ScopeFacultyIds;
            q = q.Where(r => r.Student.StudentPrograms.Any(sp => facultyIds.Contains(sp.Program.Department.FacultyId)));
        }

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderBy(r => r.CourseSection.Course.Code).ThenBy(r => r.Student.StudentNumber)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new object?[]
            {
                r.Student.StudentNumber,
                r.CourseSection.Course.Code,
                r.CourseSection.Course.Name,
                r.CourseSection.SectionNumber,
                r.Status.ToString(),
                r.Grade != null ? r.Grade.ToString() : "—",
            })
            .ToListAsync(ct);

        var columns = new[]
        {
            new ReportColumn("studentNumber", "Öğrenci No", ReportColumnAlign.Left),
            new ReportColumn("courseCode", "Ders Kodu", ReportColumnAlign.Left),
            new ReportColumn("courseName", "Ders Adı", ReportColumnAlign.Left),
            new ReportColumn("sectionNumber", "Şube", ReportColumnAlign.Center),
            new ReportColumn("status", "Durum", ReportColumnAlign.Left),
            new ReportColumn("grade", "Not", ReportColumnAlign.Center),
        };

        return new ReportResult(columns, rows.Cast<IReadOnlyList<object?>>().ToList(), total);
    }
}
