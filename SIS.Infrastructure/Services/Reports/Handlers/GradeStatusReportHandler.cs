using Microsoft.EntityFrameworkCore;
using SIS.Application.Reports;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Reports.Handlers;

/// <summary>
/// "Not Durum Çizelgesi" — bir dönem/şubedeki öğrencilerin komponent (vize/final/ödev…) puanları.
/// Parametreler: termId (Term, ops.), courseSectionId (int, ops.).
/// Scope aktifse öğrencinin fakültesine göre filtreler.
/// </summary>
public sealed class GradeStatusReportHandler : IReportHandler
{
    private readonly AppDbContext _db;
    public GradeStatusReportHandler(AppDbContext db) => _db = db;

    public string Code => "not-durum-cizelgesi";

    public ReportParameterSchema GetParameterSchema() => new(new ReportParameter[]
    {
        new("termId", "Dönem", ReportParameterType.Term, Required: false),
        new("courseSectionId", "Şube (Section Id)", ReportParameterType.Int, Required: false),
    });

    public async Task<ReportResult> RunAsync(ReportRunRequest request, ReportExecutionContext ctx, CancellationToken ct)
    {
        var p = request.Parameters;
        IQueryable<StudentGrade> q = _db.Set<StudentGrade>().AsNoTracking();

        if (p.TryGetInt("termId", out var termId))
            q = q.Where(sg => sg.CourseRegistration.CourseSection.TermId == termId);
        if (p.TryGetInt("courseSectionId", out var sectionId))
            q = q.Where(sg => sg.CourseRegistration.CourseSectionId == sectionId);

        if (!ctx.IsSystemAdmin && ctx.ScopeActive && ctx.ScopeFacultyIds.Count > 0)
        {
            var facultyIds = ctx.ScopeFacultyIds;
            q = q.Where(sg => sg.CourseRegistration.Student.StudentPrograms.Any(sp =>
                facultyIds.Contains(sp.Program.Department.FacultyId)));
        }

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderBy(sg => sg.CourseRegistration.Student.StudentNumber)
            .ThenBy(sg => sg.GradeComponent.Order)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(sg => new object?[]
            {
                sg.CourseRegistration.Student.StudentNumber,
                sg.CourseRegistration.CourseSection.Course.Code,
                sg.GradeComponent.Name,
                sg.Score,
                sg.MakeupScore,
                sg.ExamAttendance.ToString(),
            })
            .ToListAsync(ct);

        var columns = new[]
        {
            new ReportColumn("studentNumber", "Öğrenci No", ReportColumnAlign.Left),
            new ReportColumn("courseCode", "Ders Kodu", ReportColumnAlign.Left),
            new ReportColumn("component", "Sınav/Kalem", ReportColumnAlign.Left),
            new ReportColumn("score", "Puan", ReportColumnAlign.Right),
            new ReportColumn("makeupScore", "Bütünleme", ReportColumnAlign.Right),
            new ReportColumn("examAttendance", "Sınav Katılımı", ReportColumnAlign.Left),
        };

        return new ReportResult(columns, rows.Cast<IReadOnlyList<object?>>().ToList(), total);
    }
}
