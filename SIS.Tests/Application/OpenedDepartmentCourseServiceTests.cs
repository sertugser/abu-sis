using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SIS.Application.OpenedDepartmentCourses.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public sealed class OpenedDepartmentCourseServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private const int UserId = 100;
    private const int StudentId = 1;
    private const int FacultyId = 1;
    private const int DepartmentId = 1;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;
    private const int TermId = 10;

    public OpenedDepartmentCourseServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    private OpenedDepartmentCourseService BuildService() =>
        new(_context, NullLogger<OpenedDepartmentCourseService>.Instance);

    private async Task SeedBaseAsync(bool activateTerm = true, bool approveFacultySchedule = true)
    {
        var now = DateTime.UtcNow;

        _context.Set<Faculty>().Add(new Faculty
        {
            Id = FacultyId, Name = "Mühendislik", Code = "MF", CreatedAt = now,
        });
        _context.Set<Department>().Add(new Department
        {
            Id = DepartmentId, Name = "Bilgisayar Mühendisliği", Code = "ISE", FacultyId = FacultyId, CreatedAt = now,
        });
        _context.Set<AcademicProgram>().Add(new AcademicProgram
        {
            Id = ProgramId, Name = "ISE", Code = "ISE", DepartmentId = DepartmentId,
            DegreeLevel = DegreeLevel.Bachelor, MaxStudyDurationSemesters = 14, TotalRequiredEcts = 240, CreatedAt = now,
        });
        _context.Set<Curriculum>().Add(new Curriculum
        {
            Id = CurriculumId, ProgramId = ProgramId, Version = "2022", Name = "ISE 2022",
            IsActive = true, Status = CurriculumStatus.Published, CreatedAt = now,
        });
        _context.Set<AcademicTerm>().Add(new AcademicTerm
        {
            Id = TermId, Name = "2025-2026 Bahar", ShortName = "Bahar", EnglishName = "Spring", ShortEnglishName = "Spring",
            Year = 2025, TermType = TermType.Spring, IsActivated = activateTerm, IsCancelled = false,
            TermStart = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = now,
        });
        _context.Set<ApplicationUser>().Add(new ApplicationUser
        {
            Id = UserId, FirstName = "Test", LastName = "Öğrenci", UserName = "test", Email = "t@t.com",
            NormalizedUserName = "TEST", NormalizedEmail = "T@T.COM",
        });
        _context.Set<Student>().Add(new Student
        {
            Id = StudentId, UserId = UserId, StudentNumber = "20220001", NationalId = "11111111111",
            Status = StudentStatus.Active, Cgpa = 3.0m, CreatedAt = now,
        });
        _context.Set<StudentProgram>().Add(new StudentProgram
        {
            StudentId = StudentId, ProgramId = ProgramId, CurriculumId = CurriculumId,
            Type = StudentProgramType.Main, IsActive = true,
            EnrollmentDate = new DateTime(2022, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        });

        if (approveFacultySchedule)
        {
            _context.Set<FacultyScheduleApproval>().Add(new FacultyScheduleApproval
            {
                FacultyId = FacultyId, TermId = TermId, ApprovedByUserId = UserId, ApprovedAt = now,
            });
        }

        await _context.SaveChangesAsync();
    }

    private async Task AddCourseAsync(int id, string code, string name, int? departmentId = null)
    {
        _context.Set<Course>().Add(new Course
        {
            Id = id, Code = code, Name = name, Ects = 5, NationalCredit = 3,
            TheoryHours = 3, LabHours = 0, ApplicationHours = 0,
            DepartmentId = departmentId ?? DepartmentId,
            Status = CourseStatus.Active, CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
    }

    private async Task AddCurriculumCourseAsync(int courseId, int suggestedSemester, bool isCompulsory = true)
    {
        _context.Set<CurriculumCourse>().Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId, CourseId = courseId,
            SuggestedSemester = suggestedSemester, IsCompulsory = isCompulsory,
        });
        await _context.SaveChangesAsync();
    }

    private async Task AddSectionAsync(
        int id,
        int courseId,
        int sectionNumber = 1,
        CourseSectionStatus status = CourseSectionStatus.Active,
        bool suspended = false)
    {
        _context.Set<CourseSection>().Add(new CourseSection
        {
            Id = id, CourseId = courseId, TermId = TermId, SectionNumber = sectionNumber,
            Status = status, IsEnrollmentSuspended = suspended,
            Capacity = 30, CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetForStudent_ReturnsStudentNotFound_WhenUserHasNoStudentRecord()
    {
        await SeedBaseAsync();

        var result = await BuildService().GetForStudentAsync(userId: 99999);

        result.EmptyReason.Should().Be(OpenedDepartmentCoursesEmptyReason.StudentNotFound);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForStudent_ReturnsNoMainProgram_WhenStudentHasNoActiveMain()
    {
        await SeedBaseAsync();
        // Aktif Main programı pasifleştir
        var sp = await _context.Set<StudentProgram>().FirstAsync();
        sp.IsActive = false;
        await _context.SaveChangesAsync();

        var result = await BuildService().GetForStudentAsync(UserId);

        result.EmptyReason.Should().Be(OpenedDepartmentCoursesEmptyReason.NoMainProgram);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForStudent_ReturnsNoActiveTerm_WhenNoTermActivated()
    {
        await SeedBaseAsync(activateTerm: false);

        var result = await BuildService().GetForStudentAsync(UserId);

        result.EmptyReason.Should().Be(OpenedDepartmentCoursesEmptyReason.NoActiveTerm);
        result.DepartmentName.Should().Be("Bilgisayar Mühendisliği");
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForStudent_ReturnsEmpty_WhenFacultyScheduleNotApproved()
    {
        await SeedBaseAsync(approveFacultySchedule: false);
        await AddCourseAsync(100, "ISE 322", "Business Process Support");
        await AddCurriculumCourseAsync(100, suggestedSemester: 5);
        await AddSectionAsync(1000, 100);

        var result = await BuildService().GetForStudentAsync(UserId);

        result.EmptyReason.Should().Be(OpenedDepartmentCoursesEmptyReason.Empty);
        result.TermLabel.Should().Be("2025-2026 Bahar");
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForStudent_IncludesCrossDepartmentCourseFromCurriculum()
    {
        await SeedBaseAsync();
        // CENG 446 — başka bölümün dersi ama ISE müfredatında 4. sınıf seçmeli
        var cengDeptId = 99;
        _context.Set<Department>().Add(new Department
        {
            Id = cengDeptId, Name = "CENG", Code = "CENG", FacultyId = FacultyId, CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        await AddCourseAsync(200, "CENG 446", "Deep Learning", departmentId: cengDeptId);
        await AddCurriculumCourseAsync(200, suggestedSemester: 7, isCompulsory: false);
        await AddSectionAsync(2000, 200);

        var result = await BuildService().GetForStudentAsync(UserId);

        result.Items.Should().HaveCount(1);
        result.Items[0].CourseCode.Should().Be("CENG 446");
        result.Items[0].ClassYear.Should().Be(4); // semester 7 → year 4
        result.Items[0].IsCompulsory.Should().BeFalse();
    }

    [Fact]
    public async Task GetForStudent_FallsBackToCourseCodePrefix_WhenSuggestedSemesterIsZero()
    {
        await SeedBaseAsync();
        await AddCourseAsync(300, "ISE 322", "Business Process Support");
        await AddCurriculumCourseAsync(300, suggestedSemester: 0); // 0 → fallback
        await AddSectionAsync(3000, 300);

        var result = await BuildService().GetForStudentAsync(UserId);

        result.Items.Should().HaveCount(1);
        result.Items[0].ClassYear.Should().Be(3); // ISE 322 → prefix '3' → year 3
    }

    [Fact]
    public async Task GetForStudent_HidesEnrollmentSuspendedSections()
    {
        await SeedBaseAsync();
        await AddCourseAsync(400, "ISE 222", "Data Intelligence");
        await AddCurriculumCourseAsync(400, suggestedSemester: 3);
        await AddSectionAsync(4000, 400, suspended: true);

        var result = await BuildService().GetForStudentAsync(UserId);

        result.Items.Should().BeEmpty();
        result.EmptyReason.Should().Be(OpenedDepartmentCoursesEmptyReason.Empty);
    }

    [Fact]
    public async Task GetForStudent_HidesDraftAndCancelledSections()
    {
        await SeedBaseAsync();
        await AddCourseAsync(500, "ISE 350", "Data Warehousing and Mining");
        await AddCurriculumCourseAsync(500, suggestedSemester: 5);
        await AddSectionAsync(5000, 500, status: CourseSectionStatus.Draft);
        await AddSectionAsync(5001, 500, sectionNumber: 2, status: CourseSectionStatus.Cancelled);

        var result = await BuildService().GetForStudentAsync(UserId);

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForStudent_ExcludesCoursesOutsideStudentCurriculum()
    {
        await SeedBaseAsync();
        // Ders ISE bölümünün ama öğrencinin müfredatında değil
        await AddCourseAsync(600, "ISE 999", "Bölümün açtığı ama müfredatta olmayan ders");
        // CurriculumCourse eklenmiyor — bu kasıtlı
        await AddSectionAsync(6000, 600);

        var result = await BuildService().GetForStudentAsync(UserId);

        result.Items.Should().BeEmpty(); // müfredat tabanlı filtre
    }

    [Fact]
    public async Task GetForStudent_SortsByClassYearThenCodeThenSectionNumber()
    {
        await SeedBaseAsync();
        await AddCourseAsync(700, "ISE 222", "Data Intelligence");
        await AddCurriculumCourseAsync(700, suggestedSemester: 4);
        await AddSectionAsync(7000, 700);

        await AddCourseAsync(701, "ISE 322", "Business Process Support");
        await AddCurriculumCourseAsync(701, suggestedSemester: 6);
        await AddSectionAsync(7001, 701, sectionNumber: 2);
        await AddSectionAsync(7002, 701, sectionNumber: 1);

        var result = await BuildService().GetForStudentAsync(UserId);

        result.Items.Should().HaveCount(3);
        // Year 2 → ISE 222 (sec 1) | Year 3 → ISE 322 sec 1 → ISE 322 sec 2
        result.Items[0].CourseCode.Should().Be("ISE 222");
        result.Items[1].CourseCode.Should().Be("ISE 322");
        result.Items[1].SectionNumber.Should().Be(1);
        result.Items[2].SectionNumber.Should().Be(2);
    }
}
