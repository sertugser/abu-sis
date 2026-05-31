using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.API.Controllers;
using SIS.Application.Common.Services;
using SIS.Application.CourseRegistration.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Application.StudentCard.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.API;

public class AdvisorWrapperEndpointsTests
{
    [Fact]
    public async Task GetStudentTimetable_NotAdvisee_Returns403()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);
        SeedStudent(context, studentId: 100, advisorId: 999);

        var timetableMock = new Mock<IStudentTimetableService>();
        var controller = CreateController(context, userId: 42, timetableMock);

        var result = await controller.GetStudentTimetable(100, null, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        timetableMock.Verify(
            service => service.GetTimetableAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetStudentTimetable_IsAdvisee_DelegatesAndReturnsOk()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);
        SeedStudent(context, studentId: 100, advisorId: 7);

        var expected = new StudentTimetableDto(
            StudentName: "Ada Lovelace",
            TermId: 1,
            TermName: "2025-26 Güz",
            Sections: new List<StudentTimetableSectionDto>(),
            LunchBreakStart: "12:00",
            LunchBreakEnd: "13:00",
            Slots: new List<TimetableSlotDto>());

        var timetableMock = new Mock<IStudentTimetableService>();
        timetableMock
            .Setup(service => service.GetTimetableAsync(100, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(context, userId: 42, timetableMock);

        var result = await controller.GetStudentTimetable(100, null, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expected);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static void SeedInstructor(AppDbContext context, int instructorId, int userId)
    {
        if (!context.Faculties.Any(f => f.Id == 1))
        {
            context.Faculties.Add(new Faculty { Id = 1, Code = "F1", Name = "Fakülte 1" });
        }

        if (!context.Departments.Any(d => d.Id == 1))
        {
            context.Departments.Add(new Department { Id = 1, FacultyId = 1, Code = "BM", Name = "Bilgisayar Mühendisliği" });
        }

        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "ogretmen@ankarabilim.edu.tr",
            NormalizedUserName = "OGRETMEN@ANKARABILIM.EDU.TR",
            Email = "ogretmen@ankarabilim.edu.tr",
            NormalizedEmail = "OGRETMEN@ANKARABILIM.EDU.TR",
            FirstName = "Ada",
            LastName = "Lovelace"
        });

        context.Instructors.Add(new Instructor
        {
            Id = instructorId,
            UserId = userId,
            DepartmentId = 1,
            Title = "Dr.",
            MaxWeeklyHours = 12
        });

        context.SaveChanges();
    }

    private static void SeedStudent(AppDbContext context, int studentId, int advisorId)
    {
        context.Students.Add(new Student
        {
            Id = studentId,
            UserId = studentId,
            StudentNumber = $"S{studentId}",
            AdvisorId = advisorId
        });

        context.SaveChanges();
    }

    private static AdvisorController CreateController(
        AppDbContext context,
        int userId,
        Mock<IStudentTimetableService> timetableMock)
    {
        var controller = new AdvisorController(
            Mock.Of<ICourseRegistrationService>(),
            context,
            Mock.Of<ISystemConfigProvider>(),
            Mock.Of<ICurriculumStatusService>(),
            Mock.Of<IStudentTranscriptService>(),
            Mock.Of<IGpaCalculationService>(),
            timetableMock.Object,
            Mock.Of<IDepartmentScheduleService>(),
            Mock.Of<ITranscriptScenarioService>(),
            Mock.Of<IRegistrationPlanPdfService>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                ], "TestAuth"))
            }
        };

        return controller;
    }
}
