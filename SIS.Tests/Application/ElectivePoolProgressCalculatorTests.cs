using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class ElectivePoolProgressCalculatorTests
{
    private static AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void AddReg(AppDbContext ctx, int id, int studentId, int poolId, int courseId,
        RegistrationStatus status, GradeCode? grade, int ects)
    {
        ctx.Courses.Add(new Course { Id = courseId, Code = $"C{courseId}", Name = "c", Ects = ects });
        ctx.CourseSections.Add(new CourseSection { Id = id, CourseId = courseId, TermId = 1, Status = CourseSectionStatus.Active });
        ctx.CourseRegistrations.Add(new CourseRegistration
        {
            Id = id, StudentId = studentId, CourseSectionId = id,
            AppliedElectivePoolId = poolId, Status = status, Grade = grade, EctsSnapshot = ects
        });
    }

    [Fact]
    public async Task Calculate_CountsOnlyPassed_ExcludesDraftAndFailedAndWithdrawn()
    {
        using var ctx = CreateContext();
        AddReg(ctx, 1, 5, 100, 1, RegistrationStatus.Active, GradeCode.BA, 6);
        AddReg(ctx, 2, 5, 100, 2, RegistrationStatus.Draft, null, 6);
        AddReg(ctx, 3, 5, 100, 3, RegistrationStatus.Active, GradeCode.FF, 6);
        AddReg(ctx, 4, 5, 100, 4, RegistrationStatus.Withdrawn, GradeCode.W, 6);
        await ctx.SaveChangesAsync();
        var calc = new ElectivePoolProgressCalculator(ctx);

        var result = await calc.CalculateAsync(5, new[] { 100 });

        result[100].CompletedCourseCount.Should().Be(1);
        result[100].EarnedEcts.Should().Be(6);
    }

    [Fact]
    public async Task Calculate_ReturnsZero_ForPoolWithNoPassedRegs()
    {
        using var ctx = CreateContext();
        AddReg(ctx, 1, 5, 200, 1, RegistrationStatus.Draft, null, 5);
        await ctx.SaveChangesAsync();
        var calc = new ElectivePoolProgressCalculator(ctx);

        var result = await calc.CalculateAsync(5, new[] { 200 });

        result[200].CompletedCourseCount.Should().Be(0);
        result[200].EarnedEcts.Should().Be(0);
    }
}
