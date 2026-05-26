using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Reports;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Reports.Handlers;

namespace SIS.Tests.Reports;

public class AdvisorAndSectionReportTests
{
    private static AppDbContext Ctx() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdvSecRpt-{Guid.NewGuid()}").Options);

    private static ReportExecutionContext AdminCtx() =>
        new(UserId: 1, Roles: new[] { "SystemAdministrator" }, IsSystemAdmin: true,
            ScopeActive: false, ScopeFacultyIds: Array.Empty<int>());

    [Fact]
    public async Task AdvisorStudents_FiltersByAdvisor_ExcludesUnassigned()
    {
        using var ctx = Ctx();
        ctx.Students.AddRange(
            new Student { Id = 1, UserId = 11, StudentNumber = "2021001", Status = StudentStatus.Active, AdvisorId = 7 },
            new Student { Id = 2, UserId = 12, StudentNumber = "2021002", Status = StudentStatus.Active, AdvisorId = 9 },
            new Student { Id = 3, UserId = 13, StudentNumber = "2021003", Status = StudentStatus.Active, AdvisorId = null });
        await ctx.SaveChangesAsync();

        var handler = new AdvisorStudentsReportHandler(ctx);
        var req = new ReportRunRequest(new Dictionary<string, string?> { ["advisorId"] = "7" });

        var result = await handler.RunAsync(req, AdminCtx(), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Rows.Single()[0].Should().Be("2021001");
    }

    [Fact]
    public async Task SectionQuota_FiltersByTerm_ProjectsCapacity()
    {
        using var ctx = Ctx();
        ctx.Courses.Add(new Course { Id = 1, Code = "FIZ101", Name = "Fizik I" });
        ctx.CourseSections.AddRange(
            new CourseSection { Id = 10, CourseId = 1, TermId = 100, SectionNumber = 1, Capacity = 40, EnrolledCount = 35, Status = CourseSectionStatus.Active },
            new CourseSection { Id = 20, CourseId = 1, TermId = 200, SectionNumber = 2, Capacity = 30, EnrolledCount = 10, Status = CourseSectionStatus.Active });
        await ctx.SaveChangesAsync();

        var handler = new SectionQuotaReportHandler(ctx);
        var req = new ReportRunRequest(new Dictionary<string, string?> { ["termId"] = "100" });

        var result = await handler.RunAsync(req, AdminCtx(), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        var row = result.Rows.Single();
        row[0].Should().Be("FIZ101");
        row[3].Should().Be(35);   // kayıtlı
        row[4].Should().Be(40);   // kapasite
    }
}
