using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.API.Authorization;
using SIS.API.Controllers;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.API;

public class StudentManagementControllerTests
{
    [Fact]
    public async Task GetStudents_WhenTranscriptClosedForAcademicStaff_MasksTranscriptSummary()
    {
        await using var context = CreateContext();
        SeedDepartmentManager(context, userId: 42, instructorId: 7, departmentId: 1);

        var serviceMock = new Mock<IAdminStudentService>();
        serviceMock
            .Setup(x => x.GetStudentsAsync(It.IsAny<AdminStudentFilterDto>()))
            .ReturnsAsync(new PagedResult<AdminStudentListItemDto>(
                new List<AdminStudentListItemDto>
                {
                    new(5, "2024001", "Ali", "Veli", "ali@ankarabilim.edu.tr", 3.12m, 4, "Active", "Yazılım", "YZM", "Yazılım Mühendisliği", "Dr. Ada")
                },
                1,
                1,
                25));

        var configMock = new Mock<ISystemConfigProvider>();
        configMock.Setup(x => x.GetBoolAsync("Instructor.TranscriptClosed", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = CreateController(context, serviceMock.Object, configMock.Object, 42, "DepartmentManager");

        var result = await controller.GetStudents(new AdminStudentFilterDto(null, null, null, null, null, null, 1, 25));

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"TranscriptAccessible\":false");
        json.Should().Contain("\"Cgpa\":null");
        json.Should().Contain("\"CompletedSemesters\":null");
    }

    [Fact]
    public async Task UpdateAcademic_ReturnsForbid_WhenCallerCannotAccessStudentDepartment()
    {
        await using var context = CreateContext();
        SeedStudentInDepartment(context, studentId: 5, programId: 10, departmentId: 1);

        var serviceMock = new Mock<IAdminStudentService>();
        var scopeGuardMock = new Mock<IAcademicScopeGuard>();
        scopeGuardMock
            .Setup(x => x.CanAccessDepartmentAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = CreateControllerWithScopeGuard(
            context, serviceMock.Object, scopeGuardMock.Object, userId: 42, "DepartmentManager");

        var dto = new UpdateStudentAcademicDto(
            Status: "Active",
            AdvisorId: null,
            ProgramId: 10,
            CurriculumId: 1,
            ProgramType: "Main",
            IsScholarshipStudent: false,
            HasFinancialBlock: false);

        var result = await controller.UpdateAcademic(5, dto);

        result.Should().BeOfType<ForbidResult>();
        serviceMock.Verify(
            s => s.UpdateAcademicAsync(It.IsAny<int>(), It.IsAny<UpdateStudentAcademicDto>()),
            Times.Never);
    }

    private static void SeedStudentInDepartment(AppDbContext context, int studentId, int programId, int departmentId)
    {
        context.Faculties.Add(new Faculty
        {
            Id = 1,
            Code = "F1",
            Name = "Mühendislik"
        });
        context.Departments.Add(new Department
        {
            Id = departmentId,
            FacultyId = 1,
            Code = "YZM",
            Name = "Yazılım Mühendisliği"
        });
        context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = programId,
            DepartmentId = departmentId,
            Code = "YZM-LIS",
            Name = "Yazılım Mühendisliği Lisans"
        });
        context.Set<StudentProgram>().Add(new StudentProgram
        {
            StudentId = studentId,
            ProgramId = programId,
            CurriculumId = 1,
            Type = SIS.Domain.Enums.StudentProgramType.Main,
            IsActive = true
        });
        context.SaveChanges();
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static void SeedDepartmentManager(AppDbContext context, int userId, int instructorId, int departmentId)
    {
        context.Faculties.Add(new Faculty
        {
            Id = 1,
            Code = "F1",
            Name = "Mühendislik"
        });
        context.Departments.Add(new Department
        {
            Id = departmentId,
            FacultyId = 1,
            Code = "YZM",
            Name = "Yazılım Mühendisliği"
        });
        context.Instructors.Add(new Instructor
        {
            Id = instructorId,
            UserId = userId,
            DepartmentId = departmentId,
            Title = "Dr.",
            MaxWeeklyHours = 12
        });
        context.SaveChanges();
    }

    private static StudentManagementController CreateController(
        AppDbContext context,
        IAdminStudentService service,
        ISystemConfigProvider config,
        int userId,
        params string[] roles)
    {
        var controller = new StudentManagementController(service, context, config);
        AttachUser(controller, userId, roles);
        return controller;
    }

    private static StudentManagementController CreateControllerWithScopeGuard(
        AppDbContext context,
        IAdminStudentService service,
        IAcademicScopeGuard scopeGuard,
        int userId,
        params string[] roles)
    {
        var controller = new StudentManagementController(service, context, scopeGuard);
        AttachUser(controller, userId, roles);
        return controller;
    }

    private static void AttachUser(StudentManagementController controller, int userId, string[] roles)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    }.Concat(roles.Select(role => new Claim(ClaimTypes.Role, role))),
                    "TestAuth"))
            }
        };
    }
}
