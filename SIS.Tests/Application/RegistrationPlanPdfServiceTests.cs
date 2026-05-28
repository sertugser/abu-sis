using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using SIS.Application.Common.Time;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

/// <summary>
/// "Seçilen Dersler" PDF servisi için TDD testi.
/// Servis gerçek AppDbContext (InMemory) üzerinde çalışır — ICourseHistoryPdfService'in
/// test yaklaşımını taklit eder (DI-less, concrete ctor bağımlılıkları ile).
/// </summary>
public sealed class RegistrationPlanPdfServiceTests : IDisposable
{
    private const int FacultyId    = 1;
    private const int DepartmentId = 1;
    private const int ProgramId    = 1;
    private const int CurriculumId = 1;
    private const int TermId       = 1;
    private const int StudentId    = 1;
    private const int AdvisorId    = 10;
    private const int StudentUserId = 200;

    private readonly AppDbContext _context;
    private readonly FakeClock    _clock;

    public RegistrationPlanPdfServiceTests()
    {
        // QuestPDF community licence — must be set before any document generation.
        QuestPDF.Settings.License = LicenseType.Community;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _clock   = new FakeClock { UtcNow = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc) };

        SeedData();
    }

    [Fact]
    public async Task GenerateAsync_ReturnsValidPdfBytes_ForStudentWithCurrentTermRegistrations()
    {
        var service = new RegistrationPlanPdfService(_context, _clock);

        var result = await service.GenerateAsync(StudentId);

        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0, "PDF içeriği boş olmamalı");

        // PDF magic header: %PDF
        result[0].Should().Be(0x25, "PDF, '%' karakteri ile başlamalı");
        result[1].Should().Be(0x50, "PDF, 'P' karakteri ile başlamalı");
        result[2].Should().Be(0x44, "PDF, 'D' karakteri ile başlamalı");
        result[3].Should().Be(0x46, "PDF, 'F' karakteri ile başlamalı");
    }

    [Fact]
    public async Task GenerateAsync_ThrowsInvalidOperationException_WhenStudentDoesNotExist()
    {
        var service = new RegistrationPlanPdfService(_context, _clock);

        Func<Task> act = () => service.GenerateAsync(9999);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ──────────────────────────────────────────────────────────────────────
    private void SeedData()
    {
        _context.Users.Add(new ApplicationUser
        {
            Id        = StudentUserId,
            UserName  = "student1",
            Email     = "student1@test.com",
            FirstName = "Ali",
            LastName  = "Kaya"
        });

        _context.Faculties.Add(new Faculty { Id = FacultyId, Name = "Mühendislik", Code = "MUH" });
        _context.Departments.Add(new Department
        {
            Id        = DepartmentId,
            Name      = "Bilgisayar Mühendisliği",
            Code      = "CENG",
            FacultyId = FacultyId
        });
        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id                        = ProgramId,
            Name                      = "Bilgisayar Mühendisliği",
            Code                      = "CENG-BSC",
            DepartmentId              = DepartmentId,
            DegreeLevel               = DegreeLevel.Bachelor,
            EducationType             = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts         = 240
        });
        _context.Curricula.Add(new Curriculum
        {
            Id        = CurriculumId,
            ProgramId = ProgramId,
            Version   = "2024",
            IsActive  = true
        });

        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id              = TermId,
            Name            = "2025-2026 Bahar",
            ShortName       = "2025 Bahar",
            EnglishName     = "2025-2026 Spring",
            ShortEnglishName = "2025 Spring",
            Year            = 2026,
            TermType        = TermType.Spring,
            TermStart       = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            TermEnd         = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc),
            IsActivated     = true
        });

        _context.Instructors.Add(new Instructor
        {
            Id            = AdvisorId,
            UserId        = 100,
            DepartmentId  = DepartmentId,
            Title         = "Dr.",
            MaxWeeklyHours = 20
        });

        _context.Students.Add(new Student
        {
            Id                   = StudentId,
            UserId               = StudentUserId,
            StudentNumber        = "20260001",
            NationalId           = "12345678901",
            Status               = StudentStatus.Active,
            Cgpa                 = 2.80m,
            Sgpa                 = 2.80m,
            CompletedSemesters   = 2,
            IsScholarshipStudent = false,
            HasFinancialBlock    = false,
            AdvisorId            = AdvisorId
        });

        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId      = StudentId,
            ProgramId      = ProgramId,
            CurriculumId   = CurriculumId,
            Type           = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow.AddYears(-1),
            IsActive       = true
        });

        // Ders 1: Active kayıt
        var course1 = new Course
        {
            Id            = 101,
            Code          = "CENG201",
            Name          = "Veri Yapıları",
            DepartmentId  = DepartmentId,
            Ects          = 5,
            TheoryHours   = 3,
            LabHours      = 1,
            NationalCredit = 3
        };
        // Ders 2: PendingAdvisorApproval kayıt
        var course2 = new Course
        {
            Id            = 102,
            Code          = "CENG202",
            Name          = "Algoritmalar",
            DepartmentId  = DepartmentId,
            Ects          = 6,
            TheoryHours   = 3,
            LabHours      = 0,
            NationalCredit = 3
        };
        _context.Courses.AddRange(course1, course2);

        var section1 = new CourseSection
        {
            Id            = 1001,
            CourseId      = course1.Id,
            TermId        = TermId,
            SectionNumber = 1,
            Capacity      = 40,
            EnrolledCount = 10,
            Status        = CourseSectionStatus.Active,
            InstructorId  = AdvisorId
        };
        var section2 = new CourseSection
        {
            Id            = 1002,
            CourseId      = course2.Id,
            TermId        = TermId,
            SectionNumber = 1,
            Capacity      = 40,
            EnrolledCount = 5,
            Status        = CourseSectionStatus.Active,
            InstructorId  = AdvisorId
        };
        _context.CourseSections.AddRange(section1, section2);

        _context.CourseRegistrations.AddRange(
            new CourseRegistration
            {
                Id              = 5001,
                StudentId       = StudentId,
                CourseSectionId = section1.Id,
                Status          = RegistrationStatus.Active,
                EctsSnapshot    = 5,
                CreditSnapshot  = 3,
                CreatedAt       = DateTime.UtcNow
            },
            new CourseRegistration
            {
                Id              = 5002,
                StudentId       = StudentId,
                CourseSectionId = section2.Id,
                Status          = RegistrationStatus.PendingAdvisorApproval,
                EctsSnapshot    = null, // null → Course.Ects kullanılır
                CreditSnapshot  = null,
                CreatedAt       = DateTime.UtcNow
            }
        );

        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();
}
