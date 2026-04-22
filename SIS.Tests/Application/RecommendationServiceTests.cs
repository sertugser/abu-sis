using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class RecommendationServiceTests
{
    private const int FacultyId = 1;
    private const int DepartmentId = 1;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;
    private const int StudentId = 1;
    private const int StudentUserId = 100;
    private const int ActiveTermId = 1;
    private const int PastTermId = 2;

    [Fact]
    public async Task GetStudentRecommendations_MustRetakeCourse_IsCategorizedAsMandatoryWithReason()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var prerequisiteCourse = AddCourse(context, 101, "CENG101", "Programming I");
        var dependentCourse = AddCourse(context, 102, "CENG201", "Programming II");

        AddCurriculumCourse(context, prerequisiteCourse.Id, 1);
        AddCurriculumCourse(context, dependentCourse.Id, 2);
        AddPrerequisite(context, dependentCourse.Id, prerequisiteCourse.Id);

        var failedSection = AddSection(context, 1000, prerequisiteCourse.Id, PastTermId);
        AddRegistration(context, 1, StudentId, failedSection.Id, RegistrationStatus.Completed, GradeCode.FF);

        var gpaServiceMock = new Mock<IGpaCalculationService>();
        gpaServiceMock.Setup(service => service.CalculateCgpaAsync(StudentId)).ReturnsAsync(2.40m);

        var eligibilityService = new StudentEligibilityService(context, gpaServiceMock.Object);
        var recommendationService = new RecommendationService(
            context,
            eligibilityService,
            new CourseDifficultyService(context),
            new CriticalPathService(context),
            new AdvisorConstraintService(context));

        var result = await recommendationService.GetStudentRecommendationsAsync(StudentId, 80);

        result.Recommendations.Should().ContainSingle();
        var recommendation = result.Recommendations.Single();
        recommendation.CourseCode.Should().Be("CENG101");
        recommendation.Category.Should().Be("Zorunlu Al");
        recommendation.Reasons.Should().Contain(reason => reason.Contains("FF aldin"));
        recommendation.Reasons.Should().Contain(reason => reason.Contains("mezuniyet 1 donem uzayabilir"));
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
        context.Faculties.Add(new Faculty { Id = FacultyId, Name = "Engineering", Code = "ENG" });
        context.Departments.Add(new Department
        {
            Id = DepartmentId,
            Name = "Computer Engineering",
            Code = "CENG",
            FacultyId = FacultyId
        });
        context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId,
            Name = "Computer Engineering",
            Code = "CENG-BSC",
            DepartmentId = DepartmentId,
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240
        });
        context.Curricula.Add(new Curriculum
        {
            Id = CurriculumId,
            ProgramId = ProgramId,
            Version = "2024",
            IsActive = true
        });
        context.Users.Add(new ApplicationUser
        {
            Id = StudentUserId,
            UserName = "student",
            NormalizedUserName = "STUDENT",
            Email = "student@example.com",
            FirstName = "Test",
            LastName = "Student"
        });
        context.Students.Add(new Student
        {
            Id = StudentId,
            UserId = StudentUserId,
            StudentNumber = "20240001",
            NationalId = "12345678901",
            Status = StudentStatus.Active,
            Cgpa = 2.40m,
            Sgpa = 2.50m,
            CompletedSemesters = 1,
            IsScholarshipStudent = false,
            HasFinancialBlock = false
        });
        context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = StudentId,
            ProgramId = ProgramId,
            CurriculumId = CurriculumId,
            Type = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow.AddYears(-1),
            IsActive = true
        });
        var activeTermEnd = DateTime.UtcNow.AddMonths(4);
        context.AcademicTerms.Add(new AcademicTerm
        {
            Id = ActiveTermId,
            Name = "2025-2026 Spring",
            Year = 2026,
            Semester = SemesterType.Spring,
            RegistrationStart = DateTime.UtcNow.AddDays(-5),
            RegistrationEnd = DateTime.UtcNow.AddDays(5),
            AddDropStart = DateTime.UtcNow.AddDays(6),
            AddDropEnd = DateTime.UtcNow.AddDays(12),
            WithdrawalDeadline = DateTime.UtcNow.AddDays(40),
            TermStart = DateTime.UtcNow,
            TermEnd = activeTermEnd,
            Status = TermStatus.Ready,
            GradeEntryStart = activeTermEnd.AddDays(-14),
            GradeEntryEnd = activeTermEnd.AddDays(14)
        });
        context.AcademicTerms.Add(new AcademicTerm
        {
            Id = PastTermId,
            Name = "2025-2026 Fall",
            Year = 2025,
            Semester = SemesterType.Fall,
            RegistrationStart = DateTime.UtcNow.AddMonths(-5),
            RegistrationEnd = DateTime.UtcNow.AddMonths(-5).AddDays(7),
            AddDropStart = DateTime.UtcNow.AddMonths(-5).AddDays(8),
            AddDropEnd = DateTime.UtcNow.AddMonths(-5).AddDays(14),
            WithdrawalDeadline = DateTime.UtcNow.AddMonths(-4),
            TermStart = DateTime.UtcNow.AddMonths(-5),
            TermEnd = DateTime.UtcNow.AddMonths(-1),
            Status = TermStatus.Closed,
            GradeEntryStart = DateTime.UtcNow.AddMonths(-1).AddDays(-14),
            GradeEntryEnd = DateTime.UtcNow.AddMonths(-1).AddDays(14)
        });
        context.SaveChanges();
    }

    private static Course AddCourse(AppDbContext context, int id, string code, string name)
    {
        var course = new Course
        {
            Id = id,
            Code = code,
            Name = name,
            DepartmentId = DepartmentId,
            Ects = 6,
            TheoryHours = 3,
            LabHours = 0
        };
        context.Courses.Add(course);
        context.SaveChanges();
        return course;
    }

    private static void AddCurriculumCourse(AppDbContext context, int courseId, int suggestedSemester)
    {
        context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId,
            CourseId = courseId,
            IsCompulsory = true,
            SuggestedSemester = suggestedSemester
        });
        context.SaveChanges();
    }

    private static void AddPrerequisite(AppDbContext context, int courseId, int requiredCourseId)
    {
        context.CoursePrerequisites.Add(new CoursePrerequisite
        {
            CourseId = courseId,
            RequiredCourseId = requiredCourseId
        });
        context.SaveChanges();
    }

    private static CourseSection AddSection(AppDbContext context, int id, int courseId, int termId)
    {
        var section = new CourseSection
        {
            Id = id,
            CourseId = courseId,
            TermId = termId,
            SectionNumber = 1,
            Capacity = 40,
            EnrolledCount = 0,
            IsActive = true
        };
        context.CourseSections.Add(section);
        context.SaveChanges();
        return section;
    }

    private static void AddRegistration(
        AppDbContext context,
        int id,
        int studentId,
        int sectionId,
        RegistrationStatus status,
        GradeCode grade)
    {
        context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = id,
            StudentId = studentId,
            CourseSectionId = sectionId,
            Status = status,
            Grade = grade,
            CreatedAt = DateTime.UtcNow.AddMonths(-2)
        });
        context.SaveChanges();
    }
}
