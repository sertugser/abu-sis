using Microsoft.EntityFrameworkCore;
using SIS.Application.Reports;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Reports.Handlers;

/// <summary>
/// "Danışman Öğrenci Listesi" — bir danışmana (Instructor) atanmış öğrenciler.
/// Parametreler: advisorId (int, ops.), status (enum, ops.). Scope aktifse fakülteye göre filtreler.
/// </summary>
public sealed class AdvisorStudentsReportHandler : IReportHandler
{
    private readonly AppDbContext _db;
    public AdvisorStudentsReportHandler(AppDbContext db) => _db = db;

    public string Code => "danisman-ogrenci-listesi";

    public ReportParameterSchema GetParameterSchema() => new(new ReportParameter[]
    {
        new("advisorId", "Danışman (Instructor Id)", ReportParameterType.Int, Required: false),
        new("status", "Durum", ReportParameterType.Enum, Required: false,
            Options: Enum.GetNames<StudentStatus>().Select(n => new ReportParameterOption(n, n)).ToList()),
    });

    public async Task<ReportResult> RunAsync(ReportRunRequest request, ReportExecutionContext ctx, CancellationToken ct)
    {
        var p = request.Parameters;
        IQueryable<Domain.Entities.Student> q = _db.Students.AsNoTracking()
            .Where(s => s.AdvisorId != null);

        if (p.TryGetInt("advisorId", out var advisorId))
            q = q.Where(s => s.AdvisorId == advisorId);
        if (p.TryGetEnum("status", out StudentStatus status))
            q = q.Where(s => s.Status == status);

        if (!ctx.IsSystemAdmin && ctx.ScopeActive && ctx.ScopeFacultyIds.Count > 0)
        {
            var facultyIds = ctx.ScopeFacultyIds;
            q = q.Where(s => s.StudentPrograms.Any(sp => facultyIds.Contains(sp.Program.Department.FacultyId)));
        }

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderBy(s => s.AdvisorId).ThenBy(s => s.StudentNumber)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new object?[] { s.StudentNumber, s.Status.ToString(), s.Cgpa, s.AdvisorId })
            .ToListAsync(ct);

        var columns = new[]
        {
            new ReportColumn("studentNumber", "Öğrenci No", ReportColumnAlign.Left),
            new ReportColumn("status", "Durum", ReportColumnAlign.Left),
            new ReportColumn("cgpa", "GNO", ReportColumnAlign.Right),
            new ReportColumn("advisorId", "Danışman Id", ReportColumnAlign.Right),
        };

        return new ReportResult(columns, rows.Cast<IReadOnlyList<object?>>().ToList(), total);
    }
}
