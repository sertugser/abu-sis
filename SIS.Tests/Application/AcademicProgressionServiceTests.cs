using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class AcademicProgressionServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly AcademicProgressionService _service;

    private const int StudentId = 1;
    private const int ProgramId = 10;
    private const int DepartmentId = 1;
    private const int CurriculumId = 100;
    private const int TermId = 1;

    public AcademicProgressionServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _service = new AcademicProgressionService(_context, new GpaCalculationService(_context, new SIS.Tests.TestDoubles.AllVisibleGradeVisibility()));
    }

    // ══════════════════════════════════════════════════════════
    //  EvaluateStudentPromotionAsync
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task EvaluateStudentPromotionAsync_ReturnsCanPromoteTrue_WhenRuleCriteriaMet()
    {
        // Arrange: 1. sınıf öğrenci, 1→2 kuralı min CGPA 2.00 + min 30 AKTS.
        // Öğrenci 2 dersten (toplam 36 AKTS) BB ile geçmiş → CGPA 3.00, AKTS 36.
        SeedStudent(completedSemesters: 0);
        SeedRule(fromYear: 1, toYear: 2, minCgpa: 2.00m, minEcts: 30);
        SeedPassedCourse(courseId: 500, ects: 18, grade: GradeCode.BB, regId: 5000);
        SeedPassedCourse(courseId: 501, ects: 18, grade: GradeCode.BB, regId: 5001);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.EvaluateStudentPromotionAsync(StudentId);

        // Assert
        result.Should().NotBeNull();
        result!.CurrentLevel.Should().Be(1);
        result.CanPromote.Should().BeTrue();
        result.EligibleTargetLevel.Should().Be(2);
        result.Reason.Should().BeNull();
        result.Criteria.Should().Contain(c => c.Name == "CGPA" && c.Passed);
        result.Criteria.Should().Contain(c => c.Name == "AKTS" && c.Passed);
    }

    [Fact]
    public async Task EvaluateStudentPromotionAsync_ReturnsCanPromoteFalseWithReason_WhenCriterionFails()
    {
        // Arrange: 1→2 kuralı min CGPA 3.50 gerektiriyor; öğrenci 3.00 → kalır.
        SeedStudent(completedSemesters: 0);
        SeedRule(fromYear: 1, toYear: 2, minCgpa: 3.50m, minEcts: 30);
        SeedPassedCourse(courseId: 500, ects: 18, grade: GradeCode.BB, regId: 5000);
        SeedPassedCourse(courseId: 501, ects: 18, grade: GradeCode.BB, regId: 5001);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.EvaluateStudentPromotionAsync(StudentId);

        // Assert
        result.Should().NotBeNull();
        result!.CanPromote.Should().BeFalse();
        result.EligibleTargetLevel.Should().BeNull();
        result.Reason.Should().NotBeNullOrEmpty();
        result.Reason.Should().Contain("CGPA");
        result.Criteria.Should().Contain(c => c.Name == "CGPA" && !c.Passed);
    }

    [Fact]
    public async Task EvaluateStudentPromotionAsync_ReturnsNull_WhenStudentDoesNotExist()
    {
        var result = await _service.EvaluateStudentPromotionAsync(999);
        result.Should().BeNull();
    }

    // ══════════════════════════════════════════════════════════
    //  SetStudentLevelAsync
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task SetStudentLevelAsync_SetsCompletedSemestersToCleanInverse()
    {
        SeedStudent(completedSemesters: 0);
        await _context.SaveChangesAsync();

        await _service.SetStudentLevelAsync(StudentId, targetLevel: 3);

        var student = await _context.Students.FindAsync(StudentId);
        student!.CompletedSemesters.Should().Be(4); // (3-1)*2
    }

    [Fact]
    public async Task SetStudentLevelAsync_Throws_WhenTargetLevelBelowOne()
    {
        SeedStudent(completedSemesters: 2);
        await _context.SaveChangesAsync();

        var act = async () => await _service.SetStudentLevelAsync(StudentId, targetLevel: 0);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SetStudentLevelAsync_Throws_WhenStudentMissing()
    {
        var act = async () => await _service.SetStudentLevelAsync(999, targetLevel: 2);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SetStudentLevelAsync_Throws_WhenTargetExceedsProgramDuration()
    {
        // Arrange: Bachelor programı → normal süre 4 yıl. 4+5 = 9 üst sınırı aşar.
        SeedStudent(completedSemesters: 0);
        await _context.SaveChangesAsync();

        // Act
        var act = async () => await _service.SetStudentLevelAsync(StudentId, targetLevel: 9);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ══════════════════════════════════════════════════════════
    //  Seed helpers
    // ══════════════════════════════════════════════════════════

    private void SeedStudent(int completedSemesters)
    {
        var termStart = new DateTime(2024, 9, 16);
        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId,
            Name = "2024-2025 Güz",
            Year = 2024,
            TermType = TermType.Fall,
            TermStart = termStart,
            TermEnd = termStart.AddMonths(4)
        });
        _context.Faculties.Add(new Faculty { Id = 1, Code = "F1", Name = "Mühendislik" });
        _context.Departments.Add(new Department { Id = DepartmentId, FacultyId = 1, Code = "YZM", Name = "Yazılım" });
        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId,
            DepartmentId = DepartmentId,
            Code = "YZM-LIS",
            Name = "Yazılım Lisans",
            DegreeLevel = DegreeLevel.Bachelor,
            TotalRequiredEcts = 240
        });
        _context.Students.Add(new Student
        {
            Id = StudentId,
            UserId = 100,
            StudentNumber = "20240001",
            NationalId = "10000000001",
            Status = StudentStatus.Active,
            Cgpa = 0,
            Sgpa = 0,
            CompletedSemesters = completedSemesters
        });
        _context.Set<StudentProgram>().Add(new StudentProgram
        {
            StudentId = StudentId,
            ProgramId = ProgramId,
            CurriculumId = CurriculumId,
            Type = StudentProgramType.Main,
            IsActive = true
        });
    }

    private void SeedRule(int fromYear, int toYear, decimal? minCgpa = null, int? minEcts = null,
        int? minCourseCount = null, int? maxFailedCourses = null, int? minCompulsoryPassed = null)
    {
        _context.PromotionRules.Add(new PromotionRule
        {
            ProgramId = ProgramId,
            CurriculumId = null, // program-bazlı default
            FromYear = fromYear,
            ToYear = toYear,
            MinCgpa = minCgpa,
            MinEcts = minEcts,
            MinCourseCount = minCourseCount,
            MaxFailedCourses = maxFailedCourses,
            MinCompulsoryCoursePassed = minCompulsoryPassed
        });
    }

    private void SeedPassedCourse(int courseId, int ects, GradeCode grade, int regId)
    {
        _context.Courses.Add(new Course
        {
            Id = courseId,
            Code = $"C{courseId}",
            Name = $"Course {courseId}",
            Ects = ects,
            IsNonGpa = false
        });
        var sectionId = 6000 + courseId;
        _context.CourseSections.Add(new CourseSection
        {
            Id = sectionId,
            CourseId = courseId,
            TermId = TermId,
            Capacity = 40,
            EnrolledCount = 1,
            Status = CourseSectionStatus.Draft
        });
        _context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = regId,
            StudentId = StudentId,
            CourseSectionId = sectionId,
            Status = RegistrationStatus.Completed,
            Grade = grade,
            IsRepeated = false,
            MandatoryAttendance = false,
            CourseWithdrawalCount = 0,
            CreatedAt = DateTime.UtcNow
        });
    }

    public void Dispose() => _context.Dispose();
}
