using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Reports;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Reports.Handlers;

namespace SIS.Tests.Reports;

public class GradeStatusReportTests
{
    private static AppDbContext Ctx() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"GradeStatusRpt-{Guid.NewGuid()}").Options);

    private static ReportExecutionContext AdminCtx() =>
        new(UserId: 1, Roles: new[] { "SystemAdministrator" }, IsSystemAdmin: true,
            ScopeActive: false, ScopeFacultyIds: Array.Empty<int>());

    [Fact]
    public async Task GradeStatus_FiltersByTerm_ProjectsComponentScores()
    {
        using var ctx = Ctx();
        ctx.Students.Add(new Student { Id = 1, UserId = 11, StudentNumber = "2021001", Status = StudentStatus.Active });
        ctx.Courses.Add(new Course { Id = 1, Code = "FIZ101", Name = "Fizik I" });
        ctx.CourseSections.Add(new CourseSection { Id = 10, CourseId = 1, TermId = 100, SectionNumber = 1 });
        ctx.Set<CourseRegistration>().Add(new CourseRegistration { Id = 1, StudentId = 1, CourseSectionId = 10, Status = RegistrationStatus.Active });
        ctx.Set<GradeComponent>().Add(new GradeComponent { Id = 5, CourseId = 1, AcademicTermId = 100, Name = "Vize", Order = 1, Weight = 40 });
        ctx.Set<StudentGrade>().Add(new StudentGrade { Id = 1, CourseRegistrationId = 1, GradeComponentId = 5, Score = 75m });
        await ctx.SaveChangesAsync();

        var handler = new GradeStatusReportHandler(ctx);
        var req = new ReportRunRequest(new Dictionary<string, string?> { ["termId"] = "100" });

        var result = await handler.RunAsync(req, AdminCtx(), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        var row = result.Rows.Single();
        row[0].Should().Be("2021001");
        row[1].Should().Be("FIZ101");
        row[2].Should().Be("Vize");
        row[3].Should().Be(75m);
    }
}
