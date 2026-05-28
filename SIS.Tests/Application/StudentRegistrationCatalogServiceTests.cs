using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Time;

namespace SIS.Tests.Application;

public sealed class StudentRegistrationCatalogServiceTests : IDisposable
{
    private readonly AppDbContext _context;

    public StudentRegistrationCatalogServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    // ────────────────────────────────────────────────────────────────────
    // Shared seed helper — call at the start of each test that needs it
    // ────────────────────────────────────────────────────────────────────

    private async Task<(Student student, StudentProgram sp)> SeedMinimumAsync(DateTime now)
    {
        var faculty = new Faculty { Id = 1, Name = "Mühendislik Fakültesi", Code = "MF", CreatedAt = now };
        _context.Set<Faculty>().Add(faculty);

        var dept = new Department { Id = 1, Name = "Bilgisayar Mühendisliği", Code = "CENG", FacultyId = 1, CreatedAt = now };
        _context.Set<Department>().Add(dept);

        var program = new AcademicProgram
        {
            Id = 1,
            Name = "Bilgisayar Mühendisliği",
            Code = "CENG",
            DepartmentId = 1,
            DegreeLevel = DegreeLevel.Bachelor,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240,
            CreatedAt = now,
        };
        _context.Set<AcademicProgram>().Add(program);

        var curriculum = new Curriculum
        {
            Id = 1,
            ProgramId = 1,
            Version = "2022",
            Name = "CENG 2022 Müfredatı",
            IsActive = true,
            Status = CurriculumStatus.Published,
            CreatedAt = now,
        };
        _context.Set<Curriculum>().Add(curriculum);

        var term = new AcademicTerm
        {
            Id = 10,
            Name = "2025-2026 Güz",
            ShortName = "25-26 Güz",
            EnglishName = "2025-2026 Fall",
            ShortEnglishName = "25-26 Fall",
            Year = 2025,
            TermType = TermType.Fall,
            IsActivated = true,
            IsCancelled = false,
            TermStart = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = now,
        };
        _context.Set<AcademicTerm>().Add(term);

        var user = new ApplicationUser
        {
            Id = 100,
            FirstName = "Ahmet",
            LastName = "Yılmaz",
            UserName = "ahmet.yilmaz",
            Email = "ahmet.yilmaz@test.com",
            NormalizedUserName = "AHMET.YILMAZ",
            NormalizedEmail = "AHMET.YILMAZ@TEST.COM",
        };
        _context.Set<ApplicationUser>().Add(user);

        var student = new Student
        {
            Id = 1,
            UserId = 100,
            StudentNumber = "20220001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 3.10m,
            CompletedSemesters = 2,
            HasFinancialBlock = false,
            CreatedAt = now,
        };
        _context.Set<Student>().Add(student);

        var sp = new StudentProgram
        {
            StudentId = 1,
            ProgramId = 1,
            CurriculumId = 1,
            Type = StudentProgramType.Main,
            IsActive = true,
            EnrollmentDate = new DateTime(2022, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        _context.Set<StudentProgram>().Add(sp);

        await _context.SaveChangesAsync();

        return (student, sp);
    }

    private StudentRegistrationCatalogService BuildService() =>
        new StudentRegistrationCatalogService(
            _context,
            new EctsLimitResolver(new NullSystemConfigProvider()),
            new RegistrationGate(_context),
            new ElectivePoolProgressCalculator(_context),
            new SystemDateTimeProvider());

    // ────────────────────────────────────────────────────────────────────
    // Test 1 (B2 — header / empty tree)
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_ReturnsHeader_WithStudentBasics()
    {
        var now = DateTime.UtcNow;
        var (student, sp) = await SeedMinimumAsync(now);

        var result = await BuildService().BuildAsync(student, sp);

        result.Student.StudentNumber.Should().Be("20220001");
        result.Student.Cgpa.Should().Be(3.10m);
        result.Student.EctsLimit.Should().Be(48); // CGPA ≥ 2.50 → default 48
        result.Student.FinancialStatus.Should().Be("Uygun");
        result.Student.StudyDurationStatus.Should().Be("Normal Süresi İçinde");
        result.Student.ClassYear.Should().Be(2); // CompletedSemesters=2 → year=2
        result.Student.CompletedSemesters.Should().Be(2);

        result.Term.Id.Should().Be(10);
        result.Term.Year.Should().Be(2025);
    }

    // ────────────────────────────────────────────────────────────────────
    // Test 2 — tree grouping (NEW — must FAIL until Step 2 implementation)
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_GroupsCurriculumCourses_ByClassYearAndSemesterParity()
    {
        var now = DateTime.UtcNow;
        var (student, sp) = await SeedMinimumAsync(now);

        // Course 10: C101, SuggestedSemester=1 → classYear=1, parity=Guz
        var c10 = new Course
        {
            Id = 10, Code = "C101", Name = "Intro", Ects = 5, NationalCredit = 3,
            DepartmentId = 1, CreatedAt = now,
        };
        // Course 11: C202, SuggestedSemester=4 → classYear=2, parity=Bahar
        var c11 = new Course
        {
            Id = 11, Code = "C202", Name = "Algorithms", Ects = 5, NationalCredit = 3,
            DepartmentId = 1, CreatedAt = now,
        };
        _context.Set<Course>().AddRange(c10, c11);

        _context.Set<CurriculumCourse>().AddRange(
            new CurriculumCourse { CurriculumId = 1, CourseId = 10, IsCompulsory = true, SuggestedSemester = 1 },
            new CurriculumCourse { CurriculumId = 1, CourseId = 11, IsCompulsory = true, SuggestedSemester = 4 });

        await _context.SaveChangesAsync();

        var result = await BuildService().BuildAsync(student, sp);

        result.ClassYears.Should().HaveCount(2);

        var year1 = result.ClassYears.Single(y => y.ClassYear == 1);
        var guzSemester = year1.Semesters.Single(s => s.Parity == "Guz");
        guzSemester.Courses.Should().ContainSingle(r => r.Code == "C101");

        var year2 = result.ClassYears.Single(y => y.ClassYear == 2);
        var baharSemester = year2.Semesters.Single(s => s.Parity == "Bahar");
        baharSemester.Courses.Should().ContainSingle(r => r.Code == "C202");
    }

    // ────────────────────────────────────────────────────────────────────
    // Test 3 — TakeStatus + PreviousAttemptInfo (NEW — must FAIL until Step 2)
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_RetakeCourses_OnlyCurrentlyFailed()
    {
        var now = DateTime.UtcNow;
        var (student, sp) = await SeedMinimumAsync(now);

        // Two courses in curriculum
        var cFailed = new Course { Id = 20, Code = "C201", Name = "Failed Course", Ects = 5, NationalCredit = 3, DepartmentId = 1, CreatedAt = now };
        var cPassed = new Course { Id = 21, Code = "C202", Name = "Passed Course", Ects = 5, NationalCredit = 3, DepartmentId = 1, CreatedAt = now };
        _context.Set<Course>().AddRange(cFailed, cPassed);

        _context.Set<CurriculumCourse>().AddRange(
            new CurriculumCourse { CurriculumId = 1, CourseId = 20, IsCompulsory = true, SuggestedSemester = 1 },
            new CurriculumCourse { CurriculumId = 1, CourseId = 21, IsCompulsory = true, SuggestedSemester = 2 });

        // Section for both (in a past term)
        var pastSection20 = new CourseSection { Id = 300, CourseId = 20, TermId = 10, Capacity = 30, EnrolledCount = 0, SectionNumber = 1, Status = CourseSectionStatus.Active, CreatedAt = now };
        var pastSection21 = new CourseSection { Id = 301, CourseId = 21, TermId = 10, Capacity = 30, EnrolledCount = 0, SectionNumber = 1, Status = CourseSectionStatus.Active, CreatedAt = now };
        _context.Set<CourseSection>().AddRange(pastSection20, pastSection21);

        // C201 → FF (currently failed, not later passed)
        _context.Set<CourseRegistration>().Add(new CourseRegistration
        {
            Id = 10, StudentId = 1, CourseSectionId = 300,
            Status = RegistrationStatus.Completed, Grade = GradeCode.FF, CreatedAt = now
        });

        // C202 → passed (AA)
        _context.Set<CourseRegistration>().Add(new CourseRegistration
        {
            Id = 11, StudentId = 1, CourseSectionId = 301,
            Status = RegistrationStatus.Completed, Grade = GradeCode.AA, CreatedAt = now
        });

        await _context.SaveChangesAsync();

        var result = await BuildService().BuildAsync(student, sp);

        // C201 (failed) must appear in RetakeCourses
        result.RetakeCourses.Should().ContainSingle(r => r.Code == "C201");
        // C202 (passed) must NOT appear
        result.RetakeCourses.Should().NotContain(r => r.Code == "C202");
    }

    [Fact]
    public async Task BuildAsync_ElectivePoolSlots_FromCalculator()
    {
        var now = DateTime.UtcNow;
        var (student, sp) = await SeedMinimumAsync(now);

        // Seed ElectiveSubtype (required FK)
        var subtype = new ElectiveSubtypeEntity { Id = 1, Code = "TE", Name = "Teknik Seçmeli", IsActive = true, CreatedAt = now };
        _context.Set<ElectiveSubtypeEntity>().Add(subtype);

        // ElectiveGroup
        var group = new ElectiveGroup { Id = 1, Code = "TE-GRP", Name = "Teknik Seçmeli Grubu", ElectiveSubtypeId = 1, IsActive = true, CreatedAt = now };
        _context.Set<ElectiveGroup>().Add(group);

        // A course in that group
        var electiveCourse = new Course { Id = 30, Code = "E301", Name = "Elective Course", Ects = 5, NationalCredit = 3, DepartmentId = 1, CreatedAt = now };
        _context.Set<Course>().Add(electiveCourse);
        _context.Set<ElectiveGroupCourse>().Add(new ElectiveGroupCourse { ElectiveGroupId = 1, CourseId = 30, AddedAt = now });

        // CurriculumElectivePool — RequiredCourseCount=1, EctsPerCourse=null
        var pool = new CurriculumElectivePool
        {
            Id = 1, CurriculumId = 1, ElectiveGroupId = 1,
            SlotName = "TE-1", SuggestedSemesterStart = 5, SuggestedSemesterEnd = 6,
            RequiredCourseCount = 1, RequiredEcts = 0, EctsPerCourse = null,
            CreatedAt = now
        };
        _context.Set<CurriculumElectivePool>().Add(pool);

        // Past registration: student passed E301, applied to pool 1
        var pastSection = new CourseSection { Id = 400, CourseId = 30, TermId = 10, Capacity = 30, EnrolledCount = 0, SectionNumber = 1, Status = CourseSectionStatus.Active, CreatedAt = now };
        _context.Set<CourseSection>().Add(pastSection);
        _context.Set<CourseRegistration>().Add(new CourseRegistration
        {
            Id = 20, StudentId = 1, CourseSectionId = 400,
            Status = RegistrationStatus.Completed, Grade = GradeCode.AA,
            AppliedElectivePoolId = 1, CreatedAt = now
        });

        await _context.SaveChangesAsync();

        var result = await BuildService().BuildAsync(student, sp);

        result.ElectivePoolSlots.Should().HaveCount(1);
        var slot = result.ElectivePoolSlots[0];
        slot.PoolId.Should().Be(1);
        slot.GroupCode.Should().Be("TE-GRP");
        slot.SlotName.Should().Be("TE-1");
        slot.CompletedCourseCount.Should().Be(1);
        slot.IsFulfilled.Should().BeTrue(); // RequiredCourseCount=1, completed=1 → fulfilled
    }

    [Fact]
    public async Task BuildAsync_SelectedCourses_ExcludesDroppedAndWithdrawn()
    {
        var now = DateTime.UtcNow;
        var (student, sp) = await SeedMinimumAsync(now);

        // A course in curriculum
        var course = new Course { Id = 40, Code = "C401", Name = "Selected Course", Ects = 5, NationalCredit = 3, DepartmentId = 1, CreatedAt = now };
        _context.Set<Course>().Add(course);
        _context.Set<CurriculumCourse>().Add(new CurriculumCourse { CurriculumId = 1, CourseId = 40, IsCompulsory = true, SuggestedSemester = 1 });

        // 3 sections in the active term
        var secActive   = new CourseSection { Id = 500, CourseId = 40, TermId = 10, Capacity = 30, EnrolledCount = 0, SectionNumber = 1, Status = CourseSectionStatus.Active, CreatedAt = now };
        var secDropped  = new CourseSection { Id = 501, CourseId = 40, TermId = 10, Capacity = 30, EnrolledCount = 0, SectionNumber = 2, Status = CourseSectionStatus.Active, CreatedAt = now };
        var secWithdrawn = new CourseSection { Id = 502, CourseId = 40, TermId = 10, Capacity = 30, EnrolledCount = 0, SectionNumber = 3, Status = CourseSectionStatus.Active, CreatedAt = now };
        _context.Set<CourseSection>().AddRange(secActive, secDropped, secWithdrawn);

        // Active registration
        _context.Set<CourseRegistration>().Add(new CourseRegistration
        {
            Id = 30, StudentId = 1, CourseSectionId = 500,
            Status = RegistrationStatus.Active, CreatedAt = now
        });
        // Dropped — must be excluded
        _context.Set<CourseRegistration>().Add(new CourseRegistration
        {
            Id = 31, StudentId = 1, CourseSectionId = 501,
            Status = RegistrationStatus.Dropped, CreatedAt = now
        });
        // Withdrawn — must be excluded
        _context.Set<CourseRegistration>().Add(new CourseRegistration
        {
            Id = 32, StudentId = 1, CourseSectionId = 502,
            Status = RegistrationStatus.Withdrawn, CreatedAt = now
        });

        await _context.SaveChangesAsync();

        var result = await BuildService().BuildAsync(student, sp);

        result.SelectedCourses.Should().HaveCount(1);
        result.SelectedCourses[0].RegistrationId.Should().Be(30);
        result.SelectedCourses[0].Status.Should().Be("Active");
    }

    [Fact]
    public async Task BuildAsync_SetsTakeStatus_Alttan_ForFailedCourse()
    {
        var now = DateTime.UtcNow;
        var (student, sp) = await SeedMinimumAsync(now);

        // Course 10: SuggestedSemester=1 → year1, Guz
        var c10 = new Course
        {
            Id = 10, Code = "C101", Name = "Intro", Ects = 5, NationalCredit = 3,
            DepartmentId = 1, CreatedAt = now,
        };
        _context.Set<Course>().Add(c10);
        _context.Set<CurriculumCourse>().Add(
            new CurriculumCourse { CurriculumId = 1, CourseId = 10, IsCompulsory = true, SuggestedSemester = 1 });

        // Past registration in a prior term section with grade FF
        var pastSection = new CourseSection
        {
            Id = 200,
            CourseId = 10,
            TermId = 10, // same term is fine for the test — past regs query doesn't filter by term
            Capacity = 30,
            EnrolledCount = 0,
            SectionNumber = 1,
            Status = CourseSectionStatus.Active,
            CreatedAt = now,
        };
        _context.Set<CourseSection>().Add(pastSection);

        var pastReg = new CourseRegistration
        {
            Id = 1,
            StudentId = 1,
            CourseSectionId = 200,
            Status = RegistrationStatus.Completed,
            Grade = GradeCode.FF,
            CreatedAt = now,
        };
        _context.Set<CourseRegistration>().Add(pastReg);

        await _context.SaveChangesAsync();

        var result = await BuildService().BuildAsync(student, sp);

        var year1 = result.ClassYears.Single(y => y.ClassYear == 1);
        var guzSemester = year1.Semesters.Single(s => s.Parity == "Guz");
        var row = guzSemester.Courses.Single(r => r.Code == "C101");

        row.TakeStatus.Should().Be("Alttan");
        row.PreviousAttemptInfo.Should().NotBeNullOrEmpty()
            .And.Contain("FF");
    }
}
