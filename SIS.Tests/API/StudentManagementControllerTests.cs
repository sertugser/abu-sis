using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
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
