using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Reports;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Reports.Handlers;

namespace SIS.Tests.Reports;

public class GraduationAndRegistrationReportTests
{
    private static AppDbContext Ctx() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"GradRegRpt-{Guid.NewGuid()}").Options);

    private static ReportExecutionContext AdminCtx() =>
        new(UserId: 1, Roles: new[] { "SystemAdministrator" }, IsSystemAdmin: true,
            ScopeActive: false, ScopeFacultyIds: Array.Empty<int>());

    // ── Mezun Aday Listesi ────────────────────────────────────

    [Fact]
    public async Task Graduation_OnlyActive_AboveSemesterAndCgpaThreshold()
    {
        using var ctx = Ctx();
        ctx.Students.AddRange(
            new Student { Id = 1, UserId = 11, StudentNumber = "2018001", Status = StudentStatus.Active, Cgpa = 3.0m, CompletedSemesters = 8 },
            new Student { Id = 2, UserId = 12, StudentNumber = "2018002", Status = StudentStatus.Active, Cgpa = 1.5m, CompletedSemesters = 8 }, // GNO düşük
            new Student { Id = 3, UserId = 13, StudentNumber = "2021003", Status = StudentStatus.Active, Cgpa = 3.5m, CompletedSemesters = 4 }, // yarıyıl az
            new Student { Id = 4, UserId = 14, StudentNumber = "2018004", Status = StudentStatus.Graduated, Cgpa = 3.9m, CompletedSemesters = 8 }); // mezun
        await ctx.SaveChangesAsync();

        var handler = new GraduationCandidateReportHandler(ctx);
        var req = new ReportRunRequest(new Dictionary<string, string?>
        {
            ["minCompletedSemesters"] = "8",
            ["minCgpa"] = "2.0",
        });

        var result = await handler.RunAsync(req, AdminCtx(), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Rows.Single()[0].Should().Be("2018001");
    }

    [Fact]
    public void Graduation_Schema_HasExpectedParameters()
    {
        using var ctx = Ctx();
        var schema = new GraduationCandidateReportHandler(ctx).GetParameterSchema();
        schema.Parameters.Select(p => p.Key).Should().Contain(new[] { "minCompletedSemesters", "minCgpa", "programId" });
    }

    // ── Ders Kayıt Listesi ────────────────────────────────────

    [Fact]
    public async Task CourseRegistration_FiltersByTerm_AndProjectsColumns()
    {
        using var ctx = Ctx();
        ctx.Students.Add(new Student { Id = 1, UserId = 11, StudentNumber = "2021001", Status = StudentStatus.Active });
        ctx.Courses.Add(new Course { Id = 1, Code = "MAT101", Name = "Matematik I" });
        ctx.CourseSections.AddRange(
            new CourseSection { Id = 10, CourseId = 1, TermId = 100, SectionNumber = 1 },
            new CourseSection { Id = 20, CourseId = 1, TermId = 200, SectionNumber = 2 });
        ctx.Set<CourseRegistration>().AddRange(
            new CourseRegistration { Id = 1, StudentId = 1, CourseSectionId = 10, Status = RegistrationStatus.Active, Grade = GradeCode.BA },
            new CourseRegistration { Id = 2, StudentId = 1, CourseSectionId = 20, Status = RegistrationStatus.Active });
        await ctx.SaveChangesAsync();

        var handler = new CourseRegistrationReportHandler(ctx);
        var req = new ReportRunRequest(new Dictionary<string, string?> { ["termId"] = "100" });

        var result = await handler.RunAsync(req, AdminCtx(), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        var row = result.Rows.Single();
        row[0].Should().Be("2021001");   // öğrenci no
        row[1].Should().Be("MAT101");    // ders kodu
        row[3].Should().Be(1);           // şube no
        row[5].Should().Be("BA");        // not
    }
}
