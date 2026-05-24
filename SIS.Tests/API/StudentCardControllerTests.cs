using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.API.Authorization;
using SIS.API.Controllers;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.API;

public class StudentCardControllerTests
{
    [Fact]
    public async Task GetRegistrations_ReturnsForbid_WhenCallerCannotAccessDepartment()
    {
        await using var context = CreateContext();
        SeedStudentInDepartment(context, studentId: 5, programId: 10, departmentId: 1);

        var registrationMock = new Mock<ICourseRegistrationService>();
        var gpaMock = new Mock<IGpaCalculationService>();
        var scopeGuardMock = new Mock<IAcademicScopeGuard>();
        scopeGuardMock
            .Setup(x => x.CanAccessDepartmentAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = CreateController(
            context, registrationMock.Object, gpaMock.Object, scopeGuardMock.Object,
            userId: 42, "DepartmentManager");

        var result = await controller.GetRegistrations(5);

        result.Should().BeOfType<ForbidResult>();
        registrationMock.Verify(
            s => s.GetStudentRegistrationsAsync(It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task GetRegistrations_ReturnsData_WhenAccessAllowed()
    {
        await using var context = CreateContext();
        SeedStudentInDepartment(context, studentId: 5, programId: 10, departmentId: 1);

        var expected = new List<StudentRegistrationDto>
        {
            new()
            {
                RegistrationId = 99,
                CourseSectionId = 7,
                CourseCode = "YZM101",
                CourseName = "Programlamaya Giriş",
                Status = "Approved"
            }
        };

        var registrationMock = new Mock<ICourseRegistrationService>();
        registrationMock
            .Setup(x => x.GetStudentRegistrationsAsync(5))
            .ReturnsAsync(expected);

        var gpaMock = new Mock<IGpaCalculationService>();
        var scopeGuardMock = new Mock<IAcademicScopeGuard>();
        scopeGuardMock
            .Setup(x => x.CanAccessDepartmentAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = CreateController(
            context, registrationMock.Object, gpaMock.Object, scopeGuardMock.Object,
            userId: 42, "DepartmentManager");

        var result = await controller.GetRegistrations(5);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(expected);
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

    private static StudentCardController CreateController(
        AppDbContext context,
        ICourseRegistrationService registrationService,
        IGpaCalculationService gpaService,
        IAcademicScopeGuard scopeGuard,
        int userId,
        params string[] roles)
    {
        var controller = new StudentCardController(registrationService, gpaService, context, scopeGuard);
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
        return controller;
    }
}
