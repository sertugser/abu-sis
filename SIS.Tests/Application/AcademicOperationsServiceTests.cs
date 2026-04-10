using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SIS.Application.AcademicOperations.DTOs;
using SIS.Application.EnrollmentForecast.DTOs;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class AcademicOperationsServiceTests
{
    private const int FacultyId = 1;
    private const int DepartmentId = 1;
    private const int TermId = 1;

    [Fact]
    public async Task ApproveCourseSectionsAsync_DoesNotCreateSections_AndReturnsApprovedWorkflow()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var course = AddCourse(context, 1, "CENG401");
        var cacheInvalidator = new Mock<IEnrollmentForecastCacheInvalidator>();
        var service = CreateService(context, cacheInvalidator.Object);

        var result = await service.ApproveCourseSectionsAsync(
            DepartmentId,
            TermId,
            new ApproveCourseSectionsRequest
            {
                CourseId = course.Id,
                SectionCount = 3
            },
            "42",
            CancellationToken.None);

        result.Status.Should().Be(SectionApprovalStatus.Approved);
        result.ApprovedSectionCount.Should().Be(3);
        result.OpenedSectionCount.Should().Be(0);
        result.WorkflowStatus.Should().Be("Approved");
        context.CourseSections.Should().BeEmpty();
        cacheInvalidator.Verify(invalidator => invalidator.InvalidateForecastContext(), Times.Once);
    }

    [Fact]
    public async Task GetApprovalsAsync_WhenSomeApprovedSectionsAreOpened_ReturnsPartiallyOpenedWorkflow()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var course = AddCourse(context, 2, "CENG402");
        AddApproval(context, course.Id, approvedSectionCount: 3);
        AddSection(context, 1, course.Id, sectionNumber: 1);

        var service = CreateService(context);

        var result = await service.GetApprovalsAsync(DepartmentId, TermId, CancellationToken.None);

        var approval = result.Should().ContainSingle().Subject;
        approval.OpenedSectionCount.Should().Be(1);
        approval.RemainingSectionCount.Should().Be(2);
        approval.WorkflowStatus.Should().Be("PartiallyOpened");
    }

    [Fact]
    public async Task GetApprovalsAsync_WhenAllApprovedSectionsAreOpened_ReturnsCompletedWorkflow()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var course = AddCourse(context, 3, "CENG403");
        AddApproval(context, course.Id, approvedSectionCount: 2);
        AddSection(context, 2, course.Id, sectionNumber: 1);
        AddSection(context, 3, course.Id, sectionNumber: 2);

        var service = CreateService(context);

        var result = await service.GetApprovalsAsync(DepartmentId, TermId, CancellationToken.None);

        var approval = result.Should().ContainSingle().Subject;
        approval.OpenedSectionCount.Should().Be(2);
        approval.RemainingSectionCount.Should().Be(0);
        approval.WorkflowStatus.Should().Be("Completed");
    }

    private static AcademicOperationsService CreateService(
        AppDbContext context,
        IEnrollmentForecastCacheInvalidator? cacheInvalidator = null)
    {
        var forecastServiceMock = new Mock<IEnrollmentForecastService>();
        forecastServiceMock
            .Setup(service => service.GetCourseDemandForecastAsync(It.IsAny<int>()))
            .ReturnsAsync((int courseId) => new CourseDemandForecastDto
            {
                CourseId = courseId,
                CourseCode = $"COURSE-{courseId}",
                CourseName = $"Course {courseId}",
                TotalPredictedDemand = 36,
                RecommendedSectionCount = 2
            });
        forecastServiceMock
            .Setup(service => service.GetDepartmentSummaryAsync(It.IsAny<int>()))
            .ReturnsAsync((DepartmentForecastSummaryDto?)null);

        var schedulingServiceMock = new Mock<ISchedulingService>();
        var loggerMock = new Mock<ILogger<AcademicOperationsService>>();

        return new AcademicOperationsService(
            context,
            forecastServiceMock.Object,
            cacheInvalidator ?? Mock.Of<IEnrollmentForecastCacheInvalidator>(),
            schedulingServiceMock.Object,
            loggerMock.Object);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static void SeedBaseData(AppDbContext context)
    {
        context.Faculties.Add(new Faculty
        {
            Id = FacultyId,
            Name = "Engineering",
            Code = "ENG"
        });

        context.Departments.Add(new Department
        {
            Id = DepartmentId,
            FacultyId = FacultyId,
            Name = "Computer Engineering",
            Code = "CENG"
        });

        context.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId,
            Name = "2025-2026 Güz",
            ShortName = "25-26 Güz",
            EnglishName = "Fall 2025-2026",
            Year = 2025,
            Semester = SemesterType.Fall,
            RegistrationStart = DateTime.UtcNow.AddDays(-7),
            RegistrationEnd = DateTime.UtcNow.AddDays(7),
            AddDropStart = DateTime.UtcNow.AddDays(8),
            AddDropEnd = DateTime.UtcNow.AddDays(14),
            WithdrawalDeadline = DateTime.UtcNow.AddDays(30),
            TermStart = DateTime.UtcNow.AddDays(-14),
            TermEnd = DateTime.UtcNow.AddDays(90),
            IsActive = true,
            Status = TermStatus.Planning
        });

        context.SaveChanges();
    }

    private static Course AddCourse(AppDbContext context, int id, string code)
    {
        var course = new Course
        {
            Id = id,
            Code = code,
            Name = code,
            DepartmentId = DepartmentId,
            Ects = 6,
            TheoryHours = 3,
            LabHours = 0,
            IsNonGpa = false
        };

        context.Courses.Add(course);
        context.SaveChanges();
        return course;
    }

    private static void AddApproval(AppDbContext context, int courseId, int approvedSectionCount)
    {
        context.CourseSectionApprovals.Add(new CourseSectionApproval
        {
            CourseId = courseId,
            DepartmentId = DepartmentId,
            TermId = TermId,
            RecommendedSectionCount = 2,
            ApprovedSectionCount = approvedSectionCount,
            PredictedDemand = 36,
            Status = SectionApprovalStatus.Approved,
            CreatedAt = DateTime.UtcNow
        });

        context.SaveChanges();
    }

    private static void AddSection(AppDbContext context, int id, int courseId, int sectionNumber)
    {
        context.CourseSections.Add(new CourseSection
        {
            Id = id,
            CourseId = courseId,
            TermId = TermId,
            Capacity = 30,
            EnrolledCount = 0,
            SectionNumber = sectionNumber,
            IsActive = true,
            IsFinalized = false
        });

        context.SaveChanges();
    }
}
