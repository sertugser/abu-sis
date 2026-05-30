using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class RegistrationGateTests
{
    private static AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CourseSection Section(int? departmentId, CourseSectionStatus status, bool suspended)
        => new()
        {
            Id = 1, TermId = 10, Status = status, IsEnrollmentSuspended = suspended,
            Course = new Course { Id = 1, Code = "X101", Name = "Test", DepartmentId = departmentId }
        };

    [Fact]
    public async Task EvaluateSection_Blocks_WhenNotActive()
    {
        using var ctx = CreateContext();
        var result = await new RegistrationGate(ctx).EvaluateSectionAsync(Section(1, CourseSectionStatus.Draft, false));
        result.IsOpen.Should().BeFalse();
        result.Rule.Should().Be("SectionNotActive");
    }

    [Fact]
    public async Task EvaluateSection_Blocks_WhenSuspended()
    {
        using var ctx = CreateContext();
        var result = await new RegistrationGate(ctx).EvaluateSectionAsync(Section(1, CourseSectionStatus.Active, true));
        result.IsOpen.Should().BeFalse();
        result.Rule.Should().Be("EnrollmentSuspended");
    }

    [Fact]
    public async Task EvaluateSection_Blocks_WhenFacultyNotApproved()
    {
        using var ctx = CreateContext();
        ctx.Departments.Add(new Department { Id = 1, FacultyId = 7, Name = "D" });
        await ctx.SaveChangesAsync();
        var result = await new RegistrationGate(ctx).EvaluateSectionAsync(Section(1, CourseSectionStatus.Active, false));
        result.IsOpen.Should().BeFalse();
        result.Rule.Should().Be("FacultyScheduleNotApproved");
    }

    [Fact]
    public async Task EvaluateSection_Open_WhenActiveAndFacultyApproved()
    {
        using var ctx = CreateContext();
        ctx.Departments.Add(new Department { Id = 1, FacultyId = 7, Name = "D" });
        ctx.FacultyScheduleApprovals.Add(new FacultyScheduleApproval { FacultyId = 7, TermId = 10 });
        await ctx.SaveChangesAsync();
        var result = await new RegistrationGate(ctx).EvaluateSectionAsync(Section(1, CourseSectionStatus.Active, false));
        result.IsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateSection_Open_ForSharedCourse_WhenActive()
    {
        using var ctx = CreateContext();
        var result = await new RegistrationGate(ctx).EvaluateSectionAsync(Section(departmentId: null, CourseSectionStatus.Active, false));
        result.IsOpen.Should().BeTrue();
    }
}
