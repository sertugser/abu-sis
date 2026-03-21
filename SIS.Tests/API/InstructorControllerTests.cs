using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.API.Controllers;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.API;

public class InstructorControllerTests
{
    [Fact]
    public void InstructorController_HasExpectedRouteAndAuthorization()
    {
        var controllerType = typeof(InstructorController);

        var routeAttribute = controllerType.GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Single();
        var authorizeAttribute = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        routeAttribute.Template.Should().Be("api/[controller]");
        authorizeAttribute.Roles.Should().Be("Instructor");
    }

    [Fact]
    public async Task ValidateSectionSchedule_SectionNotOwned_ReturnsNotFound()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);

        var serviceMock = new Mock<ISchedulingService>(MockBehavior.Strict);
        var controller = CreateController(context, serviceMock.Object, userId: 42);

        var result = await controller.ValidateSectionSchedule(999, null, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ValidateSectionSchedule_ForcesOwnedSectionAndInstructorId()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);
        SeedSection(context, sectionId: 11, instructorId: 7);

        var expected = new ScheduleValidationResultDto { IsValid = true };
        var serviceMock = new Mock<ISchedulingService>();
        serviceMock
            .Setup(service => service.ValidateAsync(
                It.Is<ScheduleValidationRequestDto>(request =>
                    request.SectionId == 11
                    && request.InstructorId == 7
                    && request.ClassroomId == 55),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(context, serviceMock.Object, userId: 42);

        var result = await controller.ValidateSectionSchedule(
            11,
            new ScheduleValidationRequestDto
            {
                SectionId = 999,
                InstructorId = 999,
                ClassroomId = 55
            },
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task SuggestSectionSchedule_ForcesOwnedSectionAndInstructorId()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);
        SeedSection(context, sectionId: 15, instructorId: 7);

        var expected = new ScheduleSuggestionDto
        {
            SectionId = 15,
            IsFeasible = true
        };

        var serviceMock = new Mock<ISchedulingService>();
        serviceMock
            .Setup(service => service.SuggestAsync(
                It.Is<ScheduleSuggestionRequestDto>(request =>
                    request.SectionId == 15
                    && request.InstructorId == 7),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(context, serviceMock.Object, userId: 42);

        var result = await controller.SuggestSectionSchedule(
            15,
            new ScheduleSuggestionRequestDto
            {
                SectionId = 501,
                InstructorId = 501
            },
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetAvailability_ReturnsOnlyAuthenticatedInstructorRules()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);
        SeedInstructor(context, instructorId: 8, userId: 99);
        SeedAvailability(context, instructorId: 7, day: DayOfWeek.Monday, startHour: 9, endHour: 12, isPreferred: true);
        SeedAvailability(context, instructorId: 8, day: DayOfWeek.Tuesday, startHour: 13, endHour: 16, isPreferred: false);

        var controller = CreateController(context, Mock.Of<ISchedulingService>(), userId: 42);

        var result = await controller.GetAvailability(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeAssignableTo<IEnumerable<InstructorAvailabilityDto>>().Subject.ToList();
        payload.Should().HaveCount(1);
        payload[0].InstructorId.Should().Be(7);
        payload[0].DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public async Task CreateAvailability_WithInvalidTimes_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);

        var controller = CreateController(context, Mock.Of<ISchedulingService>(), userId: 42);

        var result = await controller.CreateAvailability(
            new UpsertInstructorAvailabilityDto
            {
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeOnly(13, 0),
                EndTime = new TimeOnly(12, 0),
                IsAvailable = true
            },
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateAvailability_PersistsInstructorOwnedRule()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);

        var controller = CreateController(context, Mock.Of<ISchedulingService>(), userId: 42);

        var result = await controller.CreateAvailability(
            new UpsertInstructorAvailabilityDto
            {
                DayOfWeek = DayOfWeek.Wednesday,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(12, 0),
                IsAvailable = true,
                IsPreferred = true
            },
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<InstructorAvailabilityDto>().Subject;
        payload.InstructorId.Should().Be(7);
        payload.IsPreferred.Should().BeTrue();

        context.InstructorAvailabilities.Should().ContainSingle(rule =>
            rule.InstructorId == 7
            && rule.DayOfWeek == DayOfWeek.Wednesday
            && rule.StartTime == new TimeOnly(9, 0)
            && rule.EndTime == new TimeOnly(12, 0));
    }

    [Fact]
    public async Task UpdateAvailability_RejectsRuleOwnedByAnotherInstructor()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);
        SeedInstructor(context, instructorId: 8, userId: 99);
        var otherRuleId = SeedAvailability(context, instructorId: 8, day: DayOfWeek.Friday, startHour: 10, endHour: 12, isPreferred: false);

        var controller = CreateController(context, Mock.Of<ISchedulingService>(), userId: 42);

        var result = await controller.UpdateAvailability(
            otherRuleId,
            new UpsertInstructorAvailabilityDto
            {
                DayOfWeek = DayOfWeek.Friday,
                StartTime = new TimeOnly(11, 0),
                EndTime = new TimeOnly(13, 0),
                IsAvailable = true
            },
            CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteAvailability_RemovesOwnedRule()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);
        var ruleId = SeedAvailability(context, instructorId: 7, day: DayOfWeek.Thursday, startHour: 14, endHour: 17, isPreferred: false);

        var controller = CreateController(context, Mock.Of<ISchedulingService>(), userId: 42);

        var result = await controller.DeleteAvailability(ruleId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        context.InstructorAvailabilities.Should().BeEmpty();
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

    private static void SeedSection(AppDbContext context, int sectionId, int instructorId)
    {
        context.CourseSections.Add(new CourseSection
        {
            Id = sectionId,
            CourseId = 1,
            TermId = 1,
            InstructorId = instructorId,
            Capacity = 30,
            IsActive = true,
            ScheduleJson = "[]"
        });

        context.SaveChanges();
    }

    private static int SeedAvailability(AppDbContext context, int instructorId, DayOfWeek day, int startHour, int endHour, bool isPreferred)
    {
        var entity = new InstructorAvailability
        {
            InstructorId = instructorId,
            DayOfWeek = day,
            StartTime = new TimeOnly(startHour, 0),
            EndTime = new TimeOnly(endHour, 0),
            IsAvailable = true,
            IsPreferred = isPreferred
        };

        context.InstructorAvailabilities.Add(entity);
        context.SaveChanges();
        return entity.Id;
    }

    private static InstructorController CreateController(AppDbContext context, ISchedulingService schedulingService, int userId)
    {
        var controller = new InstructorController(context, schedulingService);
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
