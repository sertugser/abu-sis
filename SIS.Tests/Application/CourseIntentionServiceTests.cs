using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.AdvisorConstraints.Services;
using SIS.Application.CourseIntention.DTOs;
using SIS.Application.StudentEligibility.DTOs;
using SIS.Application.StudentEligibility.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class CourseIntentionServiceTests
{
    private const int StudentId = 1;
    private const int SurveyId = 10;
    private const int TermId = 20;
    private const int LockedCourseId = 100;
    private const int OptionalCourseId = 101;

    [Fact]
    public async Task GetSurveyForStudent_LockedRetake_IsReturnedAsCertain()
    {
        using var context = CreateContext();
        SeedSurveyData(context);

        var service = new CourseIntentionService(
            context,
            CreateEligibilityServiceMock().Object,
            CreateAdvisorConstraintServiceMock().Object);

        var detail = await service.GetSurveyForStudentAsync(StudentId, SurveyId);

        detail.Should().NotBeNull();
        detail!.AvailableCourses.Should().ContainSingle(course => course.CourseId == LockedCourseId && course.IsLocked);
        detail.AvailableCourses.Single(course => course.CourseId == LockedCourseId).LockedIntention
            .Should().Be(CourseIntentionLevel.Certain);
        detail.AvailableCourses.Single(course => course.CourseId == LockedCourseId).LockReason
            .Should().Contain("FF");
        detail.Responses.Should().ContainSingle(response =>
            response.CourseId == LockedCourseId && response.Intention == CourseIntentionLevel.Certain);
    }

    [Fact]
    public async Task SaveResponses_LockedRetake_IsPersistedAsCertain_WhenMissingFromRequest()
    {
        using var context = CreateContext();
        SeedSurveyData(context);

        context.CourseIntentions.Add(new CourseIntention
        {
            SurveyId = SurveyId,
            StudentId = StudentId,
            CourseId = LockedCourseId,
            Intention = CourseIntentionLevel.Maybe,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var service = new CourseIntentionService(
            context,
            CreateEligibilityServiceMock().Object,
            CreateAdvisorConstraintServiceMock().Object);

        var detail = await service.SaveResponsesAsync(
            StudentId,
            new SaveCourseIntentionsRequest
            {
                SurveyId = SurveyId,
                Intentions =
                [
                    new SaveCourseIntentionItemDto
                    {
                        CourseId = OptionalCourseId,
                        Intention = CourseIntentionLevel.Maybe
                    }
                ],
                Preference = new StudentSurveyPreferenceDto
                {
                    PreferredDays = "Pazartesi",
                    PreferredTimeRanges = "Sabah"
                }
            });

        var storedIntentions = await context.CourseIntentions
            .Where(intention => intention.SurveyId == SurveyId && intention.StudentId == StudentId)
            .OrderBy(intention => intention.CourseId)
            .ToListAsync();

        storedIntentions.Should().HaveCount(2);
        storedIntentions.Single(intention => intention.CourseId == LockedCourseId).Intention
            .Should().Be(CourseIntentionLevel.Certain);
        storedIntentions.Single(intention => intention.CourseId == OptionalCourseId).Intention
            .Should().Be(CourseIntentionLevel.Maybe);

        detail.Responses.Should().Contain(response =>
            response.CourseId == LockedCourseId && response.Intention == CourseIntentionLevel.Certain);
        detail.Preference.PreferredDays.Should().Be("Pazartesi");
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static void SeedSurveyData(AppDbContext context)
    {
        context.Students.Add(new Student
        {
            Id = StudentId,
            UserId = 999,
            StudentNumber = "20240001",
            NationalId = "12345678901",
            Status = StudentStatus.Active,
            Cgpa = 2.10m,
            Sgpa = 2.30m,
            CompletedSemesters = 2,
            IsScholarshipStudent = false,
            HasFinancialBlock = false,
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        });

        context.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId,
            Name = "2025-2026 Bahar",
            ShortName = "25-26 Bahar",
            EnglishName = "Spring 2025-2026",
            Year = 2026,
            Semester = SemesterType.Spring,
            RegistrationStart = DateTime.UtcNow.AddDays(-2),
            RegistrationEnd = DateTime.UtcNow.AddDays(10),
            AddDropStart = DateTime.UtcNow.AddDays(11),
            AddDropEnd = DateTime.UtcNow.AddDays(17),
            WithdrawalDeadline = DateTime.UtcNow.AddDays(40),
            TermStart = DateTime.UtcNow.AddDays(-14),
            TermEnd = DateTime.UtcNow.AddMonths(4),
            IsActive = true,
            Status = TermStatus.RegistrationOpen,
            CreatedAt = DateTime.UtcNow.AddMonths(-1)
        });

        context.Courses.AddRange(
            new Course
            {
                Id = LockedCourseId,
                Code = "CENG201",
                Name = "Data Structures",
                Ects = 6,
                TheoryHours = 3,
                LabHours = 0,
                DepartmentId = 1,
                CreatedAt = DateTime.UtcNow.AddMonths(-12)
            },
            new Course
            {
                Id = OptionalCourseId,
                Code = "CENG205",
                Name = "Discrete Mathematics",
                Ects = 5,
                TheoryHours = 3,
                LabHours = 0,
                DepartmentId = 1,
                CreatedAt = DateTime.UtcNow.AddMonths(-12)
            });

        context.CourseIntentionSurveys.Add(new CourseIntentionSurvey
        {
            Id = SurveyId,
            TermId = TermId,
            Title = "Bahar Niyet Anketi",
            Status = CourseIntentionSurveyStatus.Open,
            OpenAtUtc = DateTime.UtcNow.AddDays(-1),
            CloseAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        });

        context.SaveChanges();
    }

    private static Mock<IStudentEligibilityService> CreateEligibilityServiceMock()
    {
        var mock = new Mock<IStudentEligibilityService>();
        mock.Setup(service => service.GetStudentEligibleCoursesAsync(StudentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                CreateEligibilityDto(
                    LockedCourseId,
                    "CENG201",
                    "Data Structures",
                    6,
                    true,
                    2,
                    "MustRetake",
                    GradeCode.FF),
                CreateEligibilityDto(
                    OptionalCourseId,
                    "CENG205",
                    "Discrete Mathematics",
                    5,
                    true,
                    2,
                    "Regular",
                    null)
            ]);
        return mock;
    }

    private static Mock<IAdvisorConstraintService> CreateAdvisorConstraintServiceMock()
    {
        var mock = new Mock<IAdvisorConstraintService>();
        mock.Setup(service => service.GetBlockedCourseIdsAsync(StudentId, TermId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        return mock;
    }

    private static StudentCourseEligibilityDto CreateEligibilityDto(
        int courseId,
        string courseCode,
        string courseName,
        int ects,
        bool isCompulsory,
        int suggestedSemester,
        string takeType,
        GradeCode? previousGrade)
    {
        return new StudentCourseEligibilityDto
        {
            StudentId = StudentId,
            StudentNumber = "20240001",
            FullName = "Test Student",
            CourseId = courseId,
            CourseCode = courseCode,
            CourseName = courseName,
            Ects = ects,
            IsCompulsory = isCompulsory,
            SuggestedSemester = suggestedSemester,
            IsEligible = true,
            TakeType = takeType,
            PreviousGrade = previousGrade,
            CurrentSemester = 2,
            Cgpa = 2.10m,
            CurrentTermEcts = 0,
            EctsLimit = 30
        };
    }
}
