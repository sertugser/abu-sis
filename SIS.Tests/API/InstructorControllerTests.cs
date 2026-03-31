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
