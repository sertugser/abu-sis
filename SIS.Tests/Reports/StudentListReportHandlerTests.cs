using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Reports;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Reports.Handlers;

namespace SIS.Tests.Reports;

public class StudentListReportHandlerTests
{
    private static AppDbContext Ctx() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"StuListRpt-{Guid.NewGuid()}").Options);

    private static ReportExecutionContext AdminCtx() =>
        new(UserId: 1, Roles: new[] { "SystemAdministrator" }, IsSystemAdmin: true,
            ScopeActive: false, ScopeFacultyIds: Array.Empty<int>());

    [Fact]
    public async Task Run_ReturnsStudents_FilteredByStatus_OrderedByNumber()
    {
        using var ctx = Ctx();
        ctx.Students.AddRange(
            new Student { Id = 1, UserId = 11, StudentNumber = "2021002", Status = StudentStatus.Active, Cgpa = 3.1m, Sgpa = 3.0m, CompletedSemesters = 4 },
            new Student { Id = 2, UserId = 12, StudentNumber = "2021001", Status = StudentStatus.Active, Cgpa = 2.5m, Sgpa = 2.4m, CompletedSemesters = 4 },
            new Student { Id = 3, UserId = 13, StudentNumber = "2020999", Status = StudentStatus.Graduated, Cgpa = 3.8m, Sgpa = 3.8m, CompletedSemesters = 8 });
        await ctx.SaveChangesAsync();

        var handler = new StudentListReportHandler(ctx);
        var req = new ReportRunRequest(
            new Dictionary<string, string?> { ["status"] = StudentStatus.Active.ToString() });

        var result = await handler.RunAsync(req, AdminCtx(), CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Rows.Should().HaveCount(2);
        // İlk kolon = öğrenci no, artan sırada
        result.Rows[0][0].Should().Be("2021001");
        result.Rows[1][0].Should().Be("2021002");
    }

    [Fact]
    public void Schema_DeclaresStatusAndProgramParameters()
    {
        using var ctx = Ctx();
        var handler = new StudentListReportHandler(ctx);
        var schema = handler.GetParameterSchema();
        schema.Parameters.Select(p => p.Key).Should().Contain(new[] { "status", "programId" });
    }
}
