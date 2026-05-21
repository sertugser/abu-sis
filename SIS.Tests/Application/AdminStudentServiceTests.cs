using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class AdminStudentServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminStudentSvc-{Guid.NewGuid()}")
            .Options);

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    [Fact]
    public async Task AssignAdvisorAsync_RejectsOptedOutInstructor_WhenSettingEnabled()
    {
        await using var context = CreateContext();
        context.Departments.Add(new Department { Id = 1, Name = "Bilgisayar", Code = "CENG", FacultyId = 1 });
        context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = 100,
            DepartmentId = 1,
            Name = "Bilgisayar Muhendisligi",
            Code = "CENG",
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240,
            CreatedAt = DateTime.UtcNow
        });
        context.Curricula.Add(new Curriculum
        {
            Id = 1000,
            ProgramId = 100,
            Version = "2024",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        context.Instructors.Add(new Instructor
        {
            Id = 10,
            UserId = 100,
            DepartmentId = 1,
            Title = "Dr.",
            MaxWeeklyHours = 10,
            AdvisorOptOutRequested = true,
        });
        context.Students.Add(new Student
        {
            Id = 20,
            UserId = 200,
            StudentNumber = "2024001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 2.5m,
            Sgpa = 2.5m,
            CompletedSemesters = 2,
        });
        context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = 20,
            ProgramId = 100,
            CurriculumId = 1000,
            Type = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var config = new Mock<ISystemConfigProvider>();
        config.Setup(x => x.GetBoolAsync("Instructor.EnableAdvisorOptOutOptions", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new AdminStudentService(context, CreateUserManagerMock().Object, config.Object);

        var action = async () => await service.AssignAdvisorAsync(20, 10);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AssignAdvisorAsync_AllowsOptedOutInstructor_WhenSettingDisabled()
    {
        await using var context = CreateContext();
        context.Departments.Add(new Department { Id = 1, Name = "Bilgisayar", Code = "CENG", FacultyId = 1 });
        context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = 100,
            DepartmentId = 1,
            Name = "Bilgisayar Muhendisligi",
            Code = "CENG",
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240,
            CreatedAt = DateTime.UtcNow
        });
        context.Curricula.Add(new Curriculum
        {
            Id = 1000,
            ProgramId = 100,
            Version = "2024",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        context.Instructors.Add(new Instructor
        {
            Id = 10,
            UserId = 100,
            DepartmentId = 1,
            Title = "Dr.",
            MaxWeeklyHours = 10,
            AdvisorOptOutRequested = true,
        });
        context.Students.Add(new Student
        {
            Id = 20,
            UserId = 200,
            StudentNumber = "2024001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 2.5m,
            Sgpa = 2.5m,
            CompletedSemesters = 2,
        });
        context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = 20,
            ProgramId = 100,
            CurriculumId = 1000,
            Type = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var config = new Mock<ISystemConfigProvider>();
        config.Setup(x => x.GetBoolAsync("Instructor.EnableAdvisorOptOutOptions", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = new AdminStudentService(context, CreateUserManagerMock().Object, config.Object);

        await service.AssignAdvisorAsync(20, 10);

        var student = await context.Students.SingleAsync(x => x.Id == 20);
        student.AdvisorId.Should().Be(10);
    }
}
