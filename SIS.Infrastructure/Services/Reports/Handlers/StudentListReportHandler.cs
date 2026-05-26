using Microsoft.EntityFrameworkCore;
using SIS.Application.Reports;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Reports.Handlers;

/// <summary>
/// "Öğrenci Listesi" raporu. Parametreler: status (enum, ops.), programId (program, ops.),
/// minCgpa/maxCgpa (decimal, ops.). Scope aktifse erişilebilir fakültelere göre filtreler.
/// </summary>
public sealed class StudentListReportHandler : IReportHandler
{
    private readonly AppDbContext _db;
    public StudentListReportHandler(AppDbContext db) => _db = db;

    public string Code => "ogrenci-listesi";

    public ReportParameterSchema GetParameterSchema() => new(new ReportParameter[]
    {
        new("status", "Durum", ReportParameterType.Enum, Required: false,
            Options: Enum.GetNames<StudentStatus>().Select(n => new ReportParameterOption(n, n)).ToList()),
        new("programId", "Program", ReportParameterType.Program, Required: false),
        new("minCgpa", "Min GNO", ReportParameterType.Decimal, Required: false),
        new("maxCgpa", "Maks GNO", ReportParameterType.Decimal, Required: false),
    });

    public async Task<ReportResult> RunAsync(ReportRunRequest request, ReportExecutionContext ctx, CancellationToken ct)
    {
        var p = request.Parameters;
        IQueryable<Domain.Entities.Student> q = _db.Students.AsNoTracking();

        if (TryEnum(p, "status", out StudentStatus status))
            q = q.Where(s => s.Status == status);

        if (TryInt(p, "programId", out var programId))
            q = q.Where(s => s.StudentPrograms.Any(sp => sp.ProgramId == programId));

        if (TryDecimal(p, "minCgpa", out var minCgpa))
            q = q.Where(s => s.Cgpa >= minCgpa);
        if (TryDecimal(p, "maxCgpa", out var maxCgpa))
            q = q.Where(s => s.Cgpa <= maxCgpa);

        // Scope: SystemAdmin değilse ve scope aktifse, erişilebilir fakültelerdeki öğrencilerle sınırla.
        if (!ctx.IsSystemAdmin && ctx.ScopeActive && ctx.ScopeFacultyIds.Count > 0)
        {
            var facultyIds = ctx.ScopeFacultyIds;
            q = q.Where(s => s.StudentPrograms.Any(sp =>
                facultyIds.Contains(sp.Program.Department.FacultyId)));
        }

        var total = await q.CountAsync(ct);

        q = (request.SortKey, request.SortDescending) switch
        {
            ("cgpa", true) => q.OrderByDescending(s => s.Cgpa),
            ("cgpa", false) => q.OrderBy(s => s.Cgpa),
            _ => q.OrderBy(s => s.StudentNumber),
        };

        var rows = await q
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new object?[] { s.StudentNumber, s.Status.ToString(), s.Cgpa, s.Sgpa, s.CompletedSemesters })
            .ToListAsync(ct);

        var columns = new[]
        {
            new ReportColumn("studentNumber", "Öğrenci No", ReportColumnAlign.Left),
            new ReportColumn("status", "Durum", ReportColumnAlign.Left),
            new ReportColumn("cgpa", "GNO", ReportColumnAlign.Right),
            new ReportColumn("sgpa", "DNO", ReportColumnAlign.Right),
            new ReportColumn("completedSemesters", "Tamamlanan Yarıyıl", ReportColumnAlign.Right),
        };

        return new ReportResult(columns, rows.Cast<IReadOnlyList<object?>>().ToList(), total);
    }

    private static bool TryEnum<TEnum>(IReadOnlyDictionary<string, string?> p, string key, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        return p.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw) && Enum.TryParse(raw, out value);
    }

    private static bool TryInt(IReadOnlyDictionary<string, string?> p, string key, out int value)
    {
        value = 0;
        return p.TryGetValue(key, out var raw) && int.TryParse(raw, out value);
    }

    private static bool TryDecimal(IReadOnlyDictionary<string, string?> p, string key, out decimal value)
    {
        value = 0m;
        return p.TryGetValue(key, out var raw)
            && decimal.TryParse(raw, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
