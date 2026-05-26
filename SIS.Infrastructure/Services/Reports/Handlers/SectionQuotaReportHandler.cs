using Microsoft.EntityFrameworkCore;
using SIS.Application.Reports;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Reports.Handlers;

/// <summary>
/// "Şube/Kontenjan Listesi" — şubelerin kapasite ve doluluk durumu.
/// Parametreler: termId (Term, ops.), status (CourseSectionStatus, ops.).
/// Scope aktifse şubenin hocasının fakültesine göre filtreler (atanmamış şubeler scope'ta düşer).
/// </summary>
public sealed class SectionQuotaReportHandler : IReportHandler
{
    private readonly AppDbContext _db;
    public SectionQuotaReportHandler(AppDbContext db) => _db = db;

    public string Code => "sube-kontenjan-listesi";

    public ReportParameterSchema GetParameterSchema() => new(new ReportParameter[]
    {
        new("termId", "Dönem", ReportParameterType.Term, Required: false),
        new("status", "Şube Durumu", ReportParameterType.Enum, Required: false,
            Options: Enum.GetNames<CourseSectionStatus>().Select(n => new ReportParameterOption(n, n)).ToList()),
    });

    public async Task<ReportResult> RunAsync(ReportRunRequest request, ReportExecutionContext ctx, CancellationToken ct)
    {
        var p = request.Parameters;
        IQueryable<Domain.Entities.CourseSection> q = _db.CourseSections.AsNoTracking();

        if (p.TryGetInt("termId", out var termId))
            q = q.Where(cs => cs.TermId == termId);
        if (p.TryGetEnum("status", out CourseSectionStatus status))
            q = q.Where(cs => cs.Status == status);

        if (!ctx.IsSystemAdmin && ctx.ScopeActive && ctx.ScopeFacultyIds.Count > 0)
        {
            var facultyIds = ctx.ScopeFacultyIds;
            q = q.Where(cs => cs.Instructor != null && facultyIds.Contains(cs.Instructor.Department.FacultyId));
        }

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderBy(cs => cs.Course.Code).ThenBy(cs => cs.SectionNumber)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(cs => new object?[]
            {
                cs.Course.Code,
                cs.Course.Name,
                cs.SectionNumber,
                cs.EnrolledCount,
                cs.Capacity,
                cs.Status.ToString(),
            })
            .ToListAsync(ct);

        var columns = new[]
        {
            new ReportColumn("courseCode", "Ders Kodu", ReportColumnAlign.Left),
            new ReportColumn("courseName", "Ders Adı", ReportColumnAlign.Left),
            new ReportColumn("sectionNumber", "Şube", ReportColumnAlign.Center),
            new ReportColumn("enrolled", "Kayıtlı", ReportColumnAlign.Right),
            new ReportColumn("capacity", "Kapasite", ReportColumnAlign.Right),
            new ReportColumn("status", "Durum", ReportColumnAlign.Left),
        };

        return new ReportResult(columns, rows.Cast<IReadOnlyList<object?>>().ToList(), total);
    }
}
