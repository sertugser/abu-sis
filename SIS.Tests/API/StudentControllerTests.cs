using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.API.Controllers;
using SIS.Application.CalendarGenerator.Services;
using SIS.Application.Common.DTOs;
using SIS.Application.Common.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.API;

public class StudentControllerTests
{
    [Fact]
    public async Task GetMe_UsesConfiguredEctsLimit()
    {
        await using var context = CreateContext();
        SeedStudentGraph(context);

        var gpaServiceMock = new Mock<IGpaCalculationService>(MockBehavior.Strict);
        var calendarRuleServiceMock = new Mock<IAcademicCalendarRuleService>(MockBehavior.Strict);
        var configProviderMock = new Mock<ISystemConfigProvider>(MockBehavior.Strict);
        configProviderMock
            .Setup(service => service.GetIntAsync("Registration.EctsLimit.CgpaBelow200", 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(30);
        configProviderMock
            .Setup(service => service.GetIntAsync("Registration.EctsLimit.Cgpa200To249", 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        configProviderMock
            .Setup(service => service.GetIntAsync("Registration.EctsLimit.CgpaAbove250", 48, It.IsAny<CancellationToken>()))
            .ReturnsAsync(60);

        var controller = new StudentController(
            context,
            gpaServiceMock.Object,
            calendarRuleServiceMock.Object,
            configProviderMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "100")
                ], "TestAuth"))
            }
        };

        var result = await controller.GetMe();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<StudentInfoDto>().Subject;
        dto.EctsLimit.Should().Be(60);
        dto.Cgpa.Should().Be(2.72m);
    }

    [Fact]
    public async Task GetTranscript_ForDocument_WhenDocumentRequestsDisabled_ReturnsForbidden()
    {
        await using var context = CreateContext();
        SeedStudentGraph(context);

        var gpaServiceMock = new Mock<IGpaCalculationService>(MockBehavior.Loose);
        var calendarRuleServiceMock = new Mock<IAcademicCalendarRuleService>(MockBehavior.Loose);
        var configProviderMock = new Mock<ISystemConfigProvider>(MockBehavior.Loose);
        var webSettingsMock = new Mock<IWebModuleSettingsProvider>(MockBehavior.Strict);
        webSettingsMock
            .Setup(service => service.GetBoolAsync("StudentPanel", "DocumentRequestEnabled", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = new StudentController(
            context,
            gpaServiceMock.Object,
            calendarRuleServiceMock.Object,
            configProviderMock.Object,
            webSettingsMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "100")
                ], "TestAuth"))
            }
        };

        var result = await controller.GetTranscript(forDocument: true);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static void SeedStudentGraph(AppDbContext context)
    {
        context.Faculties.Add(new Faculty
        {
            Id = 1,
            Name = "Engineering",
            Code = "ENG"
        });

        context.Departments.Add(new Department
        {
            Id = 1,
            FacultyId = 1,
            Name = "Computer Engineering",
            Code = "CENG"
        });

        context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = 1,
            DepartmentId = 1,
            Name = "Computer Engineering",
            Code = "CENG-BSC",
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240
        });

        context.Users.AddRange(
            new ApplicationUser
            {
                Id = 100,
                UserName = "student",
                NormalizedUserName = "STUDENT",
                Email = "student@example.com",
                FirstName = "Ayse",
                LastName = "Yilmaz"
            },
            new ApplicationUser
            {
                Id = 200,
                UserName = "advisor",
                NormalizedUserName = "ADVISOR",
                Email = "advisor@example.com",
                FirstName = "Kemal",
                LastName = "Aksoy"
            });

        context.Instructors.Add(new Instructor
        {
            Id = 10,
            UserId = 200,
            DepartmentId = 1,
            Title = "Dr.",
            MaxWeeklyHours = 12
        });

        context.Students.Add(new Student
        {
            Id = 1,
            UserId = 100,
            StudentNumber = "20220001",
            NationalId = "12345678901",
            Status = StudentStatus.Active,
            Cgpa = 2.72m,
            Sgpa = 0.00m,
            CompletedSemesters = 4,
            FrozenSemesterCount = 0,
            WithdrawalCount = 0,
            IsScholarshipStudent = false,
            HasFinancialBlock = false,
            AdvisorId = 10
        });

        context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = 1,
            ProgramId = 1,
            CurriculumId = 1,
            Type = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow.AddYears(-2),
            IsActive = true
        });

        context.SaveChanges();
    }
}
