using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Scheduling;
using Xunit;

namespace SIS.Tests.Application.Scheduling;

public sealed class StudentTimetableServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly StudentTimetableService _sut;

    private const int UserId = 100;
    private const int StudentId = 1;
    private const int FacultyId = 1;
    private const int DepartmentId = 1;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;
    private const int TermId = 10;
    private const int CourseId = 200;
    private const int ClassroomId = 300;
    private const int InstructorId = 400;
    private const int InstructorUserId = 401;

    public StudentTimetableServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new StudentTimetableService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Seed helpers ─────────────────────────────────────────────────────

    private async Task SeedBaseAsync(bool activateTerm = true)
    {
        var now = DateTime.UtcNow;

        _db.Set<Faculty>().Add(new Faculty
        {
            Id = FacultyId, Name = "Mühendislik", Code = "MF", CreatedAt = now,
        });
        _db.Set<Department>().Add(new Department
        {
            Id = DepartmentId, Name = "Bilgisayar Mühendisliği", Code = "ISE",
            FacultyId = FacultyId, CreatedAt = now,
        });
        _db.Set<AcademicProgram>().Add(new AcademicProgram
        {
            Id = ProgramId, Name = "ISE", Code = "ISE", DepartmentId = DepartmentId,
            DegreeLevel = DegreeLevel.Bachelor, MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240, CreatedAt = now,
        });
        _db.Set<Curriculum>().Add(new Curriculum
        {
            Id = CurriculumId, ProgramId = ProgramId, Version = "2022", Name = "ISE 2022",
            IsActive = true, Status = CurriculumStatus.Published, CreatedAt = now,
        });
        _db.Set<AcademicTerm>().Add(new AcademicTerm
        {
            Id = TermId, Name = "2025-2026 Bahar", ShortName = "Bahar",
            EnglishName = "Spring", ShortEnglishName = "Spring",
            Year = 2025, TermType = TermType.Spring,
            IsActivated = activateTerm, IsCancelled = false,
            TermStart = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = now,
        });
        _db.Set<ApplicationUser>().Add(new ApplicationUser
        {
            Id = UserId, FirstName = "Sertuğ", LastName = "Ser",
            UserName = "sertug", Email = "sertug@example.com",
            NormalizedUserName = "SERTUG", NormalizedEmail = "SERTUG@EXAMPLE.COM",
        });
        _db.Set<Student>().Add(new Student
        {
            Id = StudentId, UserId = UserId, StudentNumber = "20220001",
            NationalId = "11111111111", Status = StudentStatus.Active,
            Cgpa = 3.0m, CreatedAt = now,
        });
        _db.Set<StudentProgram>().Add(new StudentProgram
        {
            StudentId = StudentId, ProgramId = ProgramId, CurriculumId = CurriculumId,
            Type = StudentProgramType.Main, IsActive = true,
            EnrollmentDate = new DateTime(2022, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        });

        await _db.SaveChangesAsync();
    }

    private async Task AddCourseAsync(int id, string code, string name)
    {
        _db.Set<Course>().Add(new Course
        {
            Id = id, Code = code, Name = name,
            Ects = 5, NationalCredit = 3,
            TheoryHours = 3, LabHours = 0, ApplicationHours = 0,
            DepartmentId = DepartmentId, Status = CourseStatus.Active,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    private async Task<CourseSection> AddSectionAsync(
        int id, int courseId, int sectionNumber = 1,
        CourseSectionStatus status = CourseSectionStatus.Active)
    {
        var section = new CourseSection
        {
            Id = id, CourseId = courseId, TermId = TermId,
            SectionNumber = sectionNumber, Status = status,
            Capacity = 30, CreatedAt = DateTime.UtcNow,
        };
        _db.Set<CourseSection>().Add(section);
        await _db.SaveChangesAsync();
        return section;
    }

    private async Task AddRegistrationAsync(
        int studentId, int sectionId, RegistrationStatus status)
    {
        // CourseRegistration.TermId yok; Term, CourseSection.TermId üzerinden derive edilir.
        _db.Set<CourseRegistration>().Add(new CourseRegistration
        {
            StudentId = studentId,
            CourseSectionId = sectionId,
            Status = status,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    private async Task<Classroom> AddClassroomAsync(int id, string name)
    {
        var classroom = new Classroom
        {
            Id = id, Name = name, Capacity = 80, Type = ClassroomType.Lecture,
        };
        _db.Set<Classroom>().Add(classroom);
        await _db.SaveChangesAsync();
        return classroom;
    }

    private async Task<TimeSlot> AddTimeSlotAsync(int id, DayOfWeek day, int startHour, int startMin, int endHour, int endMin)
    {
        var slot = new TimeSlot
        {
            Id = id,
            DayOfWeek = day,
            StartTime = new TimeOnly(startHour, startMin),
            EndTime = new TimeOnly(endHour, endMin),
        };
        _db.Set<TimeSlot>().Add(slot);
        await _db.SaveChangesAsync();
        return slot;
    }

    private async Task AddSectionScheduleAsync(int sectionId, int timeSlotId, int classroomId, ScheduleBlockType blockType)
    {
        _db.Set<SectionSchedule>().Add(new SectionSchedule
        {
            CourseSectionId = sectionId,
            TimeSlotId = timeSlotId,
            ClassroomId = classroomId,
            BlockType = blockType,
        });
        await _db.SaveChangesAsync();
    }

    // ── Tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTimetableAsync_returns_only_active_registrations_in_active_sections()
    {
        await SeedBaseAsync();
        await AddCourseAsync(CourseId, "TST101", "Test Dersi");
        var active = await AddSectionAsync(1001, CourseId, sectionNumber: 1, status: CourseSectionStatus.Active);
        var draft = await AddSectionAsync(1002, CourseId, sectionNumber: 2, status: CourseSectionStatus.Draft);
        var cancelled = await AddSectionAsync(1003, CourseId, sectionNumber: 3, status: CourseSectionStatus.Cancelled);

        await AddRegistrationAsync(StudentId, active.Id, RegistrationStatus.Active);
        await AddRegistrationAsync(StudentId, draft.Id, RegistrationStatus.Active);
        await AddRegistrationAsync(StudentId, cancelled.Id, RegistrationStatus.Active);
        await AddRegistrationAsync(StudentId, active.Id, RegistrationStatus.Dropped);

        var result = await _sut.GetTimetableAsync(StudentId, TermId, CancellationToken.None);

        result.Sections.Should().HaveCount(1)
            .And.OnlyContain(s => s.SectionId == active.Id);
        result.StudentName.Should().Be("Sertuğ Ser");
        result.TermName.Should().Be("2025-2026 Bahar");
    }

    [Fact]
    public async Task GetTimetableAsync_returns_schedule_blocks_built_by_helper()
    {
        await SeedBaseAsync();
        await AddCourseAsync(CourseId, "TST101", "Test Dersi");
        var section = await AddSectionAsync(1001, CourseId);
        var classroom = await AddClassroomAsync(ClassroomId, "C 036");

        // Pzt 09:00-09:50 + Pzt 09:55-10:45 aynı derslik + theory → MERGE
        // Çar 13:00-13:50 lab → ayrı blok
        var ts1 = await AddTimeSlotAsync(501, DayOfWeek.Monday, 9, 0, 9, 50);
        var ts2 = await AddTimeSlotAsync(502, DayOfWeek.Monday, 9, 55, 10, 45);
        var ts3 = await AddTimeSlotAsync(503, DayOfWeek.Wednesday, 13, 0, 13, 50);

        await AddSectionScheduleAsync(section.Id, ts1.Id, classroom.Id, ScheduleBlockType.Theory);
        await AddSectionScheduleAsync(section.Id, ts2.Id, classroom.Id, ScheduleBlockType.Theory);
        await AddSectionScheduleAsync(section.Id, ts3.Id, classroom.Id, ScheduleBlockType.Lab);

        await AddRegistrationAsync(StudentId, section.Id, RegistrationStatus.Active);

        var result = await _sut.GetTimetableAsync(StudentId, TermId, CancellationToken.None);

        result.Sections.Should().HaveCount(1);
        var blocks = result.Sections[0].Blocks;
        blocks.Should().HaveCount(2);
        blocks.Should().ContainSingle(b =>
            b.Day == (int)DayOfWeek.Monday
            && b.StartTime == new TimeOnly(9, 0)
            && b.EndTime == new TimeOnly(10, 45)
            && b.BlockType == ScheduleBlockType.Theory);
        blocks.Should().ContainSingle(b =>
            b.Day == (int)DayOfWeek.Wednesday
            && b.BlockType == ScheduleBlockType.Lab);
    }

    [Fact]
    public async Task GetEnrolledTermsAsync_returns_terms_descending_with_active_default()
    {
        await SeedBaseAsync();
        await AddCourseAsync(CourseId, "TST101", "Test Dersi");

        // Aktif Bahar 2025'in TermStart'ını en yeni hale getir
        var activeTerm = await _db.Set<AcademicTerm>().FirstAsync(t => t.Id == TermId);
        activeTerm.TermStart = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);

        _db.Set<AcademicTerm>().AddRange(
            new AcademicTerm
            {
                Id = 11, Name = "2025-2026 Güz", ShortName = "Güz",
                EnglishName = "Fall", ShortEnglishName = "Fall",
                Year = 2025, TermType = TermType.Fall,
                IsActivated = false, IsCancelled = false,
                TermStart = new DateTime(2025, 9, 15, 0, 0, 0, DateTimeKind.Utc),
                TermEnd = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt = DateTime.UtcNow,
            },
            new AcademicTerm
            {
                Id = 12, Name = "2024-2025 Bahar", ShortName = "Bahar",
                EnglishName = "Spring", ShortEnglishName = "Spring",
                Year = 2024, TermType = TermType.Spring,
                IsActivated = false, IsCancelled = false,
                TermStart = new DateTime(2025, 2, 15, 0, 0, 0, DateTimeKind.Utc),
                TermEnd = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt = DateTime.UtcNow,
            });
        await _db.SaveChangesAsync();

        _db.Set<CourseSection>().AddRange(
            new CourseSection
            {
                Id = 1001, CourseId = CourseId, TermId = TermId,
                SectionNumber = 1, Status = CourseSectionStatus.Active,
                Capacity = 30, CreatedAt = DateTime.UtcNow,
            },
            new CourseSection
            {
                Id = 1002, CourseId = CourseId, TermId = 11,
                SectionNumber = 1, Status = CourseSectionStatus.Active,
                Capacity = 30, CreatedAt = DateTime.UtcNow,
            },
            new CourseSection
            {
                Id = 1003, CourseId = CourseId, TermId = 12,
                SectionNumber = 1, Status = CourseSectionStatus.Active,
                Capacity = 30, CreatedAt = DateTime.UtcNow,
            });
        await _db.SaveChangesAsync();

        await AddRegistrationAsync(StudentId, 1001, RegistrationStatus.Active);
        await AddRegistrationAsync(StudentId, 1002, RegistrationStatus.Active);
        await AddRegistrationAsync(StudentId, 1003, RegistrationStatus.Active);

        var terms = await _sut.GetEnrolledTermsAsync(StudentId, CancellationToken.None);

        terms.Should().HaveCount(3);
        terms[0].TermId.Should().Be(TermId);
        terms[0].IsDefault.Should().BeTrue();
        terms[1].TermId.Should().Be(11);
        terms[1].IsDefault.Should().BeFalse();
        terms[2].TermId.Should().Be(12);
    }

    [Fact]
    public async Task GeneratePdfAsync_returns_non_empty_pdf_bytes()
    {
        await SeedBaseAsync();
        await AddCourseAsync(CourseId, "TST101", "Test Dersi");
        var section = await AddSectionAsync(1001, CourseId);
        await AddRegistrationAsync(StudentId, section.Id, RegistrationStatus.Active);

        var bytes = await _sut.GeneratePdfAsync(StudentId, TermId, CancellationToken.None);

        bytes.Should().NotBeNullOrEmpty();
        bytes.Length.Should().BeGreaterThan(500);
        // PDF magic bytes "%PDF"
        bytes.Take(4).Should().Equal(new byte[] { 0x25, 0x50, 0x44, 0x46 });
    }
}
