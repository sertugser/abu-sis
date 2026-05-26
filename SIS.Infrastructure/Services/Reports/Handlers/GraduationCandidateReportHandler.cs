using Microsoft.EntityFrameworkCore;
using SIS.Application.Reports;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Reports.Handlers;

/// <summary>
/// "Mezun Aday Listesi" — tamamlanan yarıyıl ve GNO eşiğini geçen öğrenciler.
/// Parametreler: minCompletedSemesters (int, ops.), minCgpa (decimal, ops.), programId (program, ops.).
/// Scope aktifse erişilebilir fakültelere göre filtreler.
/// </summary>
public sealed class GraduationCandidateReportHandler : IReportHandler
{
    private readonly AppDbContext _db;
    public GraduationCandidateReportHandler(AppDbContext db) => _db = db;

    public string Code => "mezun-aday-listesi";

    public ReportParameterSchema GetParameterSchema() => new(new ReportParameter[]
    {
        new("minCompletedSemesters", "Min Tamamlanan Yarıyıl", ReportParameterType.Int, Required: false),
        new("minCgpa", "Min GNO", ReportParameterType.Decimal, Required: false),
        new("programId", "Program", ReportParameterType.Program, Required: false),
    });

    public async Task<ReportResult> RunAsync(ReportRunRequest request, ReportExecutionContext ctx, CancellationToken ct)
    {
        var p = request.Parameters;
        IQueryable<Domain.Entities.Student> q = _db.Students.AsNoTracking()
            .Where(s => s.Status == StudentStatus.Active);

        if (p.TryGetInt("minCompletedSemesters", out var minSem))
            q = q.Where(s => s.CompletedSemesters >= minSem);
        if (p.TryGetDecimal("minCgpa", out var minCgpa))
            q = q.Where(s => s.Cgpa >= minCgpa);
        if (p.TryGetInt("programId", out var programId))
            q = q.Where(s => s.StudentPrograms.Any(sp => sp.ProgramId == programId));

        if (!ctx.IsSystemAdmin && ctx.ScopeActive && ctx.ScopeFacultyIds.Count > 0)
        {
            var facultyIds = ctx.ScopeFacultyIds;
            q = q.Where(s => s.StudentPrograms.Any(sp => facultyIds.Contains(sp.Program.Department.FacultyId)));
        }

        var total = await q.CountAsync(ct);

        q = (request.SortKey, request.SortDescending) switch
        {
            ("cgpa", true) => q.OrderByDescending(s => s.Cgpa),
            ("cgpa", false) => q.OrderBy(s => s.Cgpa),
            _ => q.OrderByDescending(s => s.CompletedSemesters).ThenBy(s => s.StudentNumber),
        };

        var rows = await q
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new object?[] { s.StudentNumber, s.Cgpa, s.CompletedSemesters })
            .ToListAsync(ct);

        var columns = new[]
        {
            new ReportColumn("studentNumber", "Öğrenci No", ReportColumnAlign.Left),
            new ReportColumn("cgpa", "GNO", ReportColumnAlign.Right),
            new ReportColumn("completedSemesters", "Tamamlanan Yarıyıl", ReportColumnAlign.Right),
        };

        return new ReportResult(columns, rows.Cast<IReadOnlyList<object?>>().ToList(), total);
    }
}
