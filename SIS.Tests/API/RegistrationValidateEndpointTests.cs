using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseRegistration.Services;
using SIS.Application.Common.Time;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.API;

/// <summary>
/// POST /registrations/validate — Validate_DoesNotMutateState:
/// ValidateRegistrationAsync yalnızca okuma yapar; EnrolledCount ve
/// CourseRegistrations sayısını değiştirmez.
/// </summary>
public class RegistrationValidateEndpointTests
{
    // ── helpers ────────────────────────────────────────────────────────────

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IRegistrationValidationService CreateService(AppDbContext context) =>
        new RegistrationValidationService(context, new FakeClock());

    // ── tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_DoesNotMutateState()
    {
        // Arrange
        using var context = CreateContext();

        var term = new AcademicTerm
        {
            Id = 1,
            Year = 2025,
            TermType = TermType.Fall,
            TermStart = new DateTime(2025, 9, 1),
            TermEnd = new DateTime(2026, 1, 31),
            IsActivated = true,
            ShortEnglishName = "Fall 2025",
        };
        context.AcademicTerms.Add(term);

        var course = new Course
        {
            Id = 1,
            Code = "CSE101",
            Name = "Intro to CS",
            Ects = 6,
            TheoryHours = 3,
            LabHours = 0,
            CreatedAt = DateTime.UtcNow,
        };
        context.Courses.Add(course);

        var section = new CourseSection
        {
            Id = 1,
            CourseId = course.Id,
            TermId = term.Id,
            Capacity = 30,
            EnrolledCount = 5,
            SectionNumber = 1,
            Status = CourseSectionStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
        context.CourseSections.Add(section);

        var student = new Student
        {
            Id = 1,
            UserId = 99,
            StudentNumber = "S001",
            NationalId = "00000000000",
            Status = StudentStatus.Active,
            Cgpa = 3.0m,
            CompletedSemesters = 2,
            CreatedAt = DateTime.UtcNow,
        };
        context.Students.Add(student);

        await context.SaveChangesAsync();

        int enrolledCountBefore = section.EnrolledCount;
        int registrationCountBefore = await context.CourseRegistrations.CountAsync();

        var service = CreateService(context);

        // Act
        var result = await service.ValidateRegistrationAsync(
            studentId: student.Id,
            courseSectionIds: [section.Id]);

        // Assert — result is non-null and carries the IsValid flag
        result.Should().NotBeNull();

        // Assert — no state mutation
        var sectionAfter = await context.CourseSections.AsNoTracking()
            .FirstAsync(cs => cs.Id == section.Id);
        sectionAfter.EnrolledCount.Should().Be(enrolledCountBefore,
            because: "ValidateRegistrationAsync must not increment EnrolledCount");

        int registrationCountAfter = await context.CourseRegistrations.CountAsync();
        registrationCountAfter.Should().Be(registrationCountBefore,
            because: "ValidateRegistrationAsync must not insert CourseRegistration rows");
    }

    [Fact]
    public async Task Validate_ReturnsNonNull_WhenStudentMissing()
    {
        // Arrange — no student in DB
        using var context = CreateContext();
        var service = CreateService(context);

        // Act
        var result = await service.ValidateRegistrationAsync(
            studentId: 9999,
            courseSectionIds: [1]);

        // Assert — still returns a DTO (invalid), never throws
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "StudentExists");
    }
}
