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

    [Fact]
    public async Task GetStudentsAsync_HasAdvisorFalse_ReturnsOnlyStudentsWithoutAdvisor()
    {
        await using var context = CreateContext();
        context.Departments.Add(new Department { Id = 1, Name = "Bilgisayar", Code = "CENG", FacultyId = 1 });
        context.Instructors.Add(new Instructor
        {
            Id = 10,
            UserId = 100,
            DepartmentId = 1,
            Title = "Dr.",
            MaxWeeklyHours = 10,
        });
        context.Set<ApplicationUser>().Add(new ApplicationUser { Id = 100, FirstName = "Ali", LastName = "Hoca" });
        context.Set<ApplicationUser>().Add(new ApplicationUser { Id = 200, FirstName = "Ayse", LastName = "Ogrenci" });
        context.Set<ApplicationUser>().Add(new ApplicationUser { Id = 201, FirstName = "Veli", LastName = "Ogrenci" });

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
            AdvisorId = 10,
        });
        context.Students.Add(new Student
        {
            Id = 21,
            UserId = 201,
            StudentNumber = "2024002",
            NationalId = "22222222222",
            Status = StudentStatus.Active,
            Cgpa = 3.0m,
            Sgpa = 3.0m,
            CompletedSemesters = 2,
            AdvisorId = null,
        });
        await context.SaveChangesAsync();

        var config = new Mock<ISystemConfigProvider>();
        var service = new AdminStudentService(context, CreateUserManagerMock().Object, config.Object);

        var result = await service.GetStudentsAsync(
            new SIS.Application.Admin.DTOs.AdminStudentFilterDto(
                null, null, null, null, null, null, HasAdvisor: false));

        result.Items.Should().OnlyContain(i => i.AdvisorName == null);
        result.Items.Should().ContainSingle(i => i.StudentNumber == "2024002");
    }

    [Fact]
    public async Task GetStudentsAsync_FilterByCurriculumId_ReturnsMatchingStudents()
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
        context.Curricula.Add(new Curriculum { Id = 1000, ProgramId = 100, Version = "2024", IsActive = true, CreatedAt = DateTime.UtcNow });
        context.Curricula.Add(new Curriculum { Id = 1001, ProgramId = 100, Version = "2025", IsActive = true, CreatedAt = DateTime.UtcNow });
        context.Set<ApplicationUser>().Add(new ApplicationUser { Id = 200, FirstName = "Ayse", LastName = "Ogrenci" });
        context.Set<ApplicationUser>().Add(new ApplicationUser { Id = 201, FirstName = "Veli", LastName = "Ogrenci" });

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
        context.Students.Add(new Student
        {
            Id = 21,
            UserId = 201,
            StudentNumber = "2025001",
            NationalId = "22222222222",
            Status = StudentStatus.Active,
            Cgpa = 3.0m,
            Sgpa = 3.0m,
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
        context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = 21,
            ProgramId = 100,
            CurriculumId = 1001,
            Type = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var config = new Mock<ISystemConfigProvider>();
        var service = new AdminStudentService(context, CreateUserManagerMock().Object, config.Object);

        var result = await service.GetStudentsAsync(
            new SIS.Application.Admin.DTOs.AdminStudentFilterDto(
                null, null, null, null, null, null, CurriculumId: 1001));

        result.Items.Should().ContainSingle(i => i.StudentNumber == "2025001");
        result.Items.Should().OnlyContain(i => i.StudentNumber == "2025001");
    }

    [Fact]
    public async Task GetStudentsAsync_ClassYearZero_SkipsFilterAndReturnsAllStudents()
    {
        await using var context = CreateContext();
        context.Set<ApplicationUser>().Add(new ApplicationUser { Id = 200, FirstName = "Ayse", LastName = "Ogrenci" });
        context.Set<ApplicationUser>().Add(new ApplicationUser { Id = 201, FirstName = "Veli", LastName = "Ogrenci" });

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
        context.Students.Add(new Student
        {
            Id = 21,
            UserId = 201,
            StudentNumber = "2024002",
            NationalId = "22222222222",
            Status = StudentStatus.Active,
            Cgpa = 3.0m,
            Sgpa = 3.0m,
            CompletedSemesters = 4,
        });
        await context.SaveChangesAsync();

        var config = new Mock<ISystemConfigProvider>();
        var service = new AdminStudentService(context, CreateUserManagerMock().Object, config.Object);

        var result = await service.GetStudentsAsync(
            new SIS.Application.Admin.DTOs.AdminStudentFilterDto(
                null, null, null, null, null, null, ClassYear: 0));

        result.TotalCount.Should().Be(2);
        result.Items.Should().Contain(i => i.StudentNumber == "2024001");
        result.Items.Should().Contain(i => i.StudentNumber == "2024002");
    }

    [Fact]
    public async Task GetStudentByIdAsync_ReturnsUserId()
    {
        await using var context = CreateContext();
        const int studentId = 20;
        const int userId = 200;

        context.Students.Add(new Student
        {
            Id = studentId,
            UserId = userId,
            StudentNumber = "2024001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 2.5m,
            Sgpa = 2.5m,
            CompletedSemesters = 2,
        });
        await context.SaveChangesAsync();

        var config = new Mock<ISystemConfigProvider>();
        var service = new AdminStudentService(context, CreateUserManagerMock().Object, config.Object);

        var result = await service.GetStudentByIdAsync(studentId);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
    }
}
