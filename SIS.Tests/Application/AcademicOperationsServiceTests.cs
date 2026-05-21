using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Application.Scheduling.Services;
using SIS.Application.AcademicOperations.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class AcademicOperationsServiceTests
{
    [Fact]
    public async Task BulkAssignAdvisorsAsync_AssignsSelectedStudents_AndSkipsExistingAdvisor()
    {
        using var context = CreateContext();
        SeedBaseData(context);
        AddInstructor(context, id: 100, userId: 500, departmentId: 10, title: "Dr.");
        AddInstructor(context, id: 101, userId: 501, departmentId: 10, title: "Doç. Dr.");
        AddStudent(context, id: 1, userId: 601, studentNumber: "20240001", programId: 1000, curriculumId: 2000);
        AddStudent(context, id: 2, userId: 602, studentNumber: "20240002", programId: 1000, curriculumId: 2000, advisorId: 101);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var request = new BulkAdvisorAssignmentRequestDto
        {
            DepartmentId = 10,
            AdvisorId = 100,
            ReplaceExisting = false,
            StudentIds = [1, 2],
        };

        var result = await service.BulkAssignAdvisorsAsync(request, userId: 1, roles: ["AdministrativeStaff"]);

        result.Should().NotBeNull();
        result!.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
        context.Students.Find(1)!.AdvisorId.Should().Be(100);
        context.Students.Find(2)!.AdvisorId.Should().Be(101);
    }

    [Fact]
    public async Task BulkAssignAdvisorsAsync_AllowsAdvisorFromAnotherAccessibleDepartment()
    {
        using var context = CreateContext();
        SeedBaseData(context);
        AddDepartment(context, id: 20, code: "SE", name: "Yazilim Muhendisligi");
        context.Users.Add(new ApplicationUser { Id = 502, UserName = "advisor3", FirstName = "Can", LastName = "Danisman" });
        AddInstructor(context, id: 102, userId: 502, departmentId: 20, title: "Dr.");
        AddStudent(context, id: 1, userId: 601, studentNumber: "20240001", programId: 1000, curriculumId: 2000);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var request = new BulkAdvisorAssignmentRequestDto
        {
            DepartmentId = 10,
            AdvisorId = 102,
            ReplaceExisting = false,
            StudentIds = [1],
        };

        var result = await service.BulkAssignAdvisorsAsync(request, userId: 1, roles: ["AdministrativeStaff"]);

        result.Should().NotBeNull();
        result!.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(0);
        context.Students.Find(1)!.AdvisorId.Should().Be(102);
    }

    [Fact]
    public async Task BulkAssignAdvisorsAsync_RejectsAdvisorOutsideUserScope()
    {
        using var context = CreateContext();
        SeedBaseData(context);
        AddDepartment(context, id: 20, code: "SE", name: "Yazilim Muhendisligi");
        context.Users.Add(new ApplicationUser { Id = 502, UserName = "advisor3", FirstName = "Can", LastName = "Danisman" });
        AddInstructor(context, id: 100, userId: 500, departmentId: 10, title: "Dr.");
        AddInstructor(context, id: 102, userId: 502, departmentId: 20, title: "Dr.");
        AddStudent(context, id: 1, userId: 601, studentNumber: "20240001", programId: 1000, curriculumId: 2000);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var request = new BulkAdvisorAssignmentRequestDto
        {
            DepartmentId = 10,
            AdvisorId = 102,
            ReplaceExisting = false,
            StudentIds = [1],
        };

        var result = await service.BulkAssignAdvisorsAsync(request, userId: 500, roles: ["DepartmentManager"]);

        result.Should().NotBeNull();
        result!.SuccessCount.Should().Be(0);
        result.Errors.Should().ContainSingle(error => error.Contains("erisim kapsaminizda degil"));
        context.Students.Find(1)!.AdvisorId.Should().BeNull();
    }

    [Fact]
    public async Task BulkAssignCurriculumAsync_AssignsSameProgramStudents_AndReportsProgramMismatch()
    {
        using var context = CreateContext();
        SeedBaseData(context);
        AddCurriculum(context, id: 2001, programId: 1000, version: "2025");
        AddProgram(context, id: 1001, departmentId: 10, code: "SE", name: "Yazılım Mühendisliği");
        AddCurriculum(context, id: 3000, programId: 1001, version: "2024");
        AddStudent(context, id: 1, userId: 601, studentNumber: "20240001", programId: 1000, curriculumId: 2000);
        AddStudent(context, id: 2, userId: 602, studentNumber: "20240002", programId: 1001, curriculumId: 3000);
        await context.SaveChangesAsync();

        var invalidator = new Mock<IEnrollmentForecastCacheInvalidator>(MockBehavior.Strict);
        invalidator.Setup(cache => cache.InvalidateForecastContext());
        var service = CreateService(context, invalidator);
        var request = new BulkCurriculumAssignmentRequestDto
        {
            DepartmentId = 10,
            CurriculumId = 2001,
            StudentIds = [1, 2],
        };

        var result = await service.BulkAssignCurriculumAsync(request, userId: 1, roles: ["AdministrativeStaff"]);

        result.Should().NotBeNull();
        result!.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
        context.StudentPrograms.Find(1, 1000)!.CurriculumId.Should().Be(2001);
        context.StudentPrograms.Find(2, 1001)!.CurriculumId.Should().Be(3000);
        result.Results.Single(item => item.StudentId == 2).Message.Should().Contain("programina ait degil");
        invalidator.Verify(cache => cache.InvalidateForecastContext(), Times.Once);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static AcademicOperationsService CreateService(
        AppDbContext context,
        Mock<IEnrollmentForecastCacheInvalidator>? invalidator = null)
    {
        return new AcademicOperationsService(
            context,
            Mock.Of<IEnrollmentForecastService>(),
            invalidator?.Object ?? Mock.Of<IEnrollmentForecastCacheInvalidator>(),
            Mock.Of<ISchedulingService>(),
            NullLogger<AcademicOperationsService>.Instance);
    }

    private static void SeedBaseData(AppDbContext context)
    {
        context.Users.AddRange(
            new ApplicationUser { Id = 500, UserName = "advisor1", FirstName = "Ayşe", LastName = "Danışman" },
            new ApplicationUser { Id = 501, UserName = "advisor2", FirstName = "Mehmet", LastName = "Danışman" },
            new ApplicationUser { Id = 601, UserName = "student1", FirstName = "Ali", LastName = "Öğrenci" },
            new ApplicationUser { Id = 602, UserName = "student2", FirstName = "Zeynep", LastName = "Öğrenci" });

        context.Faculties.Add(new Faculty { Id = 1, Code = "ENG", Name = "Mühendislik Fakültesi" });
        context.Departments.Add(new Department { Id = 10, Code = "CENG", Name = "Bilgisayar Mühendisliği", FacultyId = 1 });
        AddProgram(context, id: 1000, departmentId: 10, code: "CENG", name: "Bilgisayar Mühendisliği");
        AddCurriculum(context, id: 2000, programId: 1000, version: "2024");
    }

    private static void AddDepartment(AppDbContext context, int id, string code, string name)
    {
        context.Departments.Add(new Department
        {
            Id = id,
            Code = code,
            Name = name,
            FacultyId = 1,
        });
    }

    private static void AddProgram(AppDbContext context, int id, int departmentId, string code, string name)
    {
        context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = id,
            DepartmentId = departmentId,
            Code = code,
            Name = name,
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240,
        });
    }

    private static void AddCurriculum(AppDbContext context, int id, int programId, string version)
    {
        context.Curricula.Add(new Curriculum
        {
            Id = id,
            ProgramId = programId,
            Version = version,
            Name = $"{version} Müfredatı",
            IsActive = version == "2024",
            Status = CurriculumStatus.Published,
        });
    }

    private static void AddInstructor(AppDbContext context, int id, int userId, int departmentId, string title)
    {
        context.Instructors.Add(new Instructor
        {
            Id = id,
            UserId = userId,
            DepartmentId = departmentId,
            Title = title,
            MaxWeeklyHours = 20,
        });
    }

    private static void AddStudent(
        AppDbContext context,
        int id,
        int userId,
        string studentNumber,
        int programId,
        int curriculumId,
        int? advisorId = null)
    {
        context.Students.Add(new Student
        {
            Id = id,
            UserId = userId,
            StudentNumber = studentNumber,
            NationalId = $"1000000000{id}",
            Status = StudentStatus.Active,
            Cgpa = 3.1m,
            CompletedSemesters = 2,
            AdvisorId = advisorId,
        });

        context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = id,
            ProgramId = programId,
            CurriculumId = curriculumId,
            Type = StudentProgramType.Main,
            IsActive = true,
            EnrollmentDate = DateTime.UtcNow.AddYears(-1),
        });
    }
}
