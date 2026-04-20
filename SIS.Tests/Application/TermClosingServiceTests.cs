using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.TermClosing.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class TermClosingServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly TermClosingService _service;

    private const int FacultyId = 1;
    private const int DepartmentId = 1;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;
    private const int TermId = 1;
    private const int UserId = 100;
    private const int StudentId = 1;
    private const int StudentUserId = 200;
    private const int CourseId = 1;
    private const int SectionId = 1;

    public TermClosingServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);

        var gpaService = new GpaCalculationService(_context);
        var progressionService = new AcademicProgressionService(_context, gpaService);
        _service = new TermClosingService(_context, progressionService, gpaService);

        SeedBaseData();
    }

    private void SeedBaseData()
    {
        _context.Faculties.Add(new Faculty { Id = FacultyId, Name = "Muhendislik", Code = "MUH" });
        _context.Departments.Add(new Department { Id = DepartmentId, Name = "Bilgisayar", Code = "BM", FacultyId = FacultyId });

        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId,
            Name = "2025-2026 Guz",
            Year = 2025,
            Semester = SemesterType.Fall,
            IsActive = true,
            Status = TermStatus.GradeEntry,
            RegistrationStart = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            RegistrationEnd = new DateTime(2025, 9, 15, 0, 0, 0, DateTimeKind.Utc),
            AddDropStart = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            AddDropEnd = new DateTime(2025, 9, 15, 0, 0, 0, DateTimeKind.Utc),
            TermStart = new DateTime(2025, 9, 15, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
        });

        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId,
            Name = "Bilgisayar Muhendisligi",
            Code = "BM",
            DepartmentId = DepartmentId,
            DegreeLevel = DegreeLevel.Bachelor,
            TotalRequiredEcts = 240
        });

        _context.Set<Curriculum>().Add(new Curriculum
        {
            Id = CurriculumId,
            ProgramId = ProgramId,
            Version = "2024",
            IsActive = true
        });

        _context.Users.Add(new ApplicationUser
        {
            Id = StudentUserId,
            UserName = "student1",
            NormalizedUserName = "STUDENT1",
            FirstName = "Ali",
            LastName = "Yilmaz",
            Email = "ali@test.com",
            NormalizedEmail = "ALI@TEST.COM"
        });

        _context.Students.Add(new Student
        {
            Id = StudentId,
            UserId = StudentUserId,
            StudentNumber = "2024BM0001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 2.50m,
            Sgpa = 2.50m,
            CompletedSemesters = 2
        });

        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = StudentId,
            ProgramId = ProgramId,
            CurriculumId = CurriculumId,
            Type = StudentProgramType.Main,
            IsActive = true,
            EnrollmentDate = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        _context.Courses.Add(new Course
        {
            Id = CourseId,
            Code = "BM101",
            Name = "Programlama",
            DepartmentId = DepartmentId,
            Ects = 6,
            TheoryHours = 3,
            LabHours = 2
        });

        _context.CourseSections.Add(new CourseSection
        {
            Id = SectionId,
            CourseId = CourseId,
            TermId = TermId,
            Capacity = 40,
            EnrolledCount = 1,
            IsActive = true,
            IsFinalized = false
        });

        _context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = 1,
            StudentId = StudentId,
            CourseSectionId = SectionId,
            Status = RegistrationStatus.Active,
            CreatedAt = DateTime.UtcNow
        });

        _context.GradeComponents.Add(new GradeComponent
        {
            Id = 1, CourseId = CourseId, AcademicTermId = TermId,
            Name = "Vize", Weight = 40, Order = 1
        });
        _context.GradeComponents.Add(new GradeComponent
        {
            Id = 2, CourseId = CourseId, AcademicTermId = TermId,
            Name = "Final", Weight = 60, Order = 2
        });

        _context.StudentGrades.Add(new StudentGrade { Id = 1, CourseRegistrationId = 1, GradeComponentId = 1, Score = 70 });
        _context.StudentGrades.Add(new StudentGrade { Id = 2, CourseRegistrationId = 1, GradeComponentId = 2, Score = 80 });

        _context.Users.Add(new ApplicationUser
        {
            Id = UserId, UserName = "manager1", NormalizedUserName = "MANAGER1",
            FirstName = "Mehmet", LastName = "Ozturk",
            Email = "mehmet@test.com", NormalizedEmail = "MEHMET@TEST.COM"
        });

        _context.GradingCriteria.Add(new GradingCriteria
        {
            Id = 1,
            CourseId = CourseId,
            AcademicTermId = TermId
        });

        _context.SaveChanges();
    }

    // Her adımı sırasıyla çalıştıran helper — testler birbirinden bağımsız olsun diye
    private async Task RunLockGrades() => await _service.LockGradesAsync(DepartmentId, UserId);
    private async Task RunCalculateGpa()
    {
        await RunLockGrades();
        await _service.CalculateGpaAsync(DepartmentId, UserId);
    }
    private async Task RunPromote()
    {
        await RunCalculateGpa();
        await _service.PromoteAsync(DepartmentId, UserId);
    }

    [Fact]
    public async Task GetStatus_ReturnsAllFourSteps()
    {
        var result = await _service.GetStatusAsync(DepartmentId);
        result.Steps.Should().HaveCount(4);
        result.Steps.Should().AllSatisfy(s => s.IsCompleted.Should().BeFalse());
        result.TermId.Should().Be(TermId);
    }

    [Fact]
    public async Task LockGrades_FinalizesAllSections()
    {
        var result = await _service.LockGradesAsync(DepartmentId, UserId);
        result.Success.Should().BeTrue();

        var section = await _context.CourseSections.FindAsync(SectionId);
        section!.IsFinalized.Should().BeTrue();

        var reg = await _context.CourseRegistrations.FindAsync(1);
        reg!.Grade.Should().NotBeNull();
    }

    [Fact]
    public async Task LockGrades_CalculatesCorrectGrade()
    {
        await _service.LockGradesAsync(DepartmentId, UserId);

        var reg = await _context.CourseRegistrations.FindAsync(1);
        reg!.Grade.Should().Be(GradeCode.BB);
    }

    [Fact]
    public async Task LockGrades_RecordsProgress()
    {
        await _service.LockGradesAsync(DepartmentId, UserId);

        var progress = await _context.TermClosingProgresses
            .FirstOrDefaultAsync(p => p.DepartmentId == DepartmentId && p.Step == TermClosingStep.GradeLock);

        progress.Should().NotBeNull();
        progress!.IsCompleted.Should().BeTrue();
        progress.CompletedByUserId.Should().Be(UserId);
    }

    [Fact]
    public async Task UnlockGrades_ClearsFinalized()
    {
        await _service.LockGradesAsync(DepartmentId, UserId);
        var result = await _service.UnlockGradesAsync(DepartmentId, UserId);

        result.Success.Should().BeTrue();

        var section = await _context.CourseSections.FindAsync(SectionId);
        section!.IsFinalized.Should().BeFalse();

        var progress = await _context.TermClosingProgresses
            .FirstOrDefaultAsync(p => p.DepartmentId == DepartmentId && p.Step == TermClosingStep.GradeLock);
        progress.Should().BeNull("ClearProgressAsync now removes the record");
    }

    [Fact]
    public async Task CalculateGpa_RecalculatesAndRecordsProgress()
    {
        await RunLockGrades();
        var result = await _service.CalculateGpaAsync(DepartmentId, UserId);
        result.TotalStudents.Should().BeGreaterThan(0);

        var progress = await _context.TermClosingProgresses
            .FirstOrDefaultAsync(p => p.DepartmentId == DepartmentId && p.Step == TermClosingStep.GpaCalculation);
        progress.Should().NotBeNull();
        progress!.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Promote_SavesSnapshotAndRecordsProgress()
    {
        await RunCalculateGpa();
        var result = await _service.PromoteAsync(DepartmentId, UserId);
        result.TotalStudentsAnalyzed.Should().BeGreaterThan(0);

        var progress = await _context.TermClosingProgresses
            .FirstOrDefaultAsync(p => p.DepartmentId == DepartmentId && p.Step == TermClosingStep.Promotion);
        progress.Should().NotBeNull();
        progress!.IsCompleted.Should().BeTrue();

        var snapshotDoc = JsonDocument.Parse(progress.Notes!);
        snapshotDoc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        snapshotDoc.RootElement.TryGetProperty(StudentId.ToString(), out var studentSnap).Should().BeTrue();
        studentSnap.TryGetProperty("CompletedSemesters", out _).Should().BeTrue();
    }

    [Fact]
    public async Task RollbackPromotion_RestoresOldValues()
    {
        var originalSemesters = (await _context.Students.FindAsync(StudentId))!.CompletedSemesters;

        await RunCalculateGpa();
        await _service.PromoteAsync(DepartmentId, UserId);
        var result = await _service.RollbackPromotionAsync(DepartmentId, UserId);

        result.Success.Should().BeTrue();

        var student = await _context.Students.FindAsync(StudentId);
        student!.CompletedSemesters.Should().Be(originalSemesters);

        var progress = await _context.TermClosingProgresses
            .FirstOrDefaultAsync(p => p.DepartmentId == DepartmentId && p.Step == TermClosingStep.Promotion);
        progress.Should().BeNull("ClearProgressAsync now removes the record");
    }

    [Fact]
    public async Task RollbackPromotion_FailsWhenNoSnapshot()
    {
        var result = await _service.RollbackPromotionAsync(DepartmentId, UserId);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task OpenNewTerm_CreatesTermAndDeactivatesOld()
    {
        await RunPromote();
        var request = new NewTermRequestDto
        {
            Name = "2025-2026 Bahar",
            Year = 2025,
            Semester = SemesterType.Spring,
            RegistrationStart = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            RegistrationEnd = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            TermStart = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        var result = await _service.OpenNewTermAsync(DepartmentId, UserId, request);

        result.Success.Should().BeTrue();
        result.NewTermId.Should().NotBeNull();

        var oldTerm = await _context.AcademicTerms.FindAsync(TermId);
        oldTerm!.IsActive.Should().BeTrue(); // current term is no longer deactivated per-department

        var newTerm = await _context.AcademicTerms.FindAsync(result.NewTermId);
        newTerm!.IsActive.Should().BeTrue();
        newTerm.Name.Should().Be("2025-2026 Bahar");
    }

    [Fact]
    public async Task OpenNewTerm_ReusesExistingTerm()
    {
        await RunPromote();
        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id = 99, Name = "2025-2026 Bahar", Year = 2025, Semester = SemesterType.Spring,
            IsActive = false,
            RegistrationStart = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            RegistrationEnd = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            AddDropStart = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            AddDropEnd = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            TermStart = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)
        });
        await _context.SaveChangesAsync();

        var request = new NewTermRequestDto
        {
            Name = "2025-2026 Bahar", Year = 2025, Semester = SemesterType.Spring,
            RegistrationStart = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            RegistrationEnd = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            TermStart = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        var result = await _service.OpenNewTermAsync(DepartmentId, UserId, request);

        result.Success.Should().BeTrue();
        result.NewTermId.Should().Be(99);

        var termCount = await _context.AcademicTerms.CountAsync();
        termCount.Should().Be(2);
    }

    [Fact]
    public async Task LockGrades_FailsWhenNoActiveTerm()
    {
        var term = await _context.AcademicTerms.FindAsync(TermId);
        term!.IsActive = false;
        await _context.SaveChangesAsync();

        var result = await _service.LockGradesAsync(DepartmentId, UserId);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Aktif donem bulunamadi");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
