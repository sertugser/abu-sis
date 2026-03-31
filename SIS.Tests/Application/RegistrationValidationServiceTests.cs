using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.Common.Services;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class RegistrationValidationServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly RegistrationValidationService _service;

    // Shared IDs for convenience
    private const int StudentId = 1;
    private const int FacultyId = 1;
    private const int DepartmentId = 1;
    private const int OtherDepartmentId = 2;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;
    private const int TermId = 1;

    public RegistrationValidationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _service = new RegistrationValidationService(_context, GetSystemConfig());

        SeedBaseData();
    }

    private void SeedBaseData()
    {
        // Faculty
        _context.Faculties.Add(new Faculty { Id = FacultyId, Name = "Engineering", Code = "ENG" });

        // Departments
        _context.Departments.Add(new Department { Id = DepartmentId, Name = "Computer Engineering", Code = "CE", FacultyId = FacultyId });
        _context.Departments.Add(new Department { Id = OtherDepartmentId, Name = "Electrical Engineering", Code = "EE", FacultyId = FacultyId });

        // Program
        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId,
            Name = "Computer Engineering",
            Code = "CE-BSC",
            DepartmentId = DepartmentId,
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240
        });

        // Curriculum
        _context.Curricula.Add(new Curriculum { Id = CurriculumId, ProgramId = ProgramId, Version = "2022", IsActive = true });

        // Active term with registration period covering "now"
        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId,
            Name = "2025-2026 Spring",
            Year = 2025,
            Semester = SemesterType.Spring,
            RegistrationStart = DateTime.UtcNow.AddDays(-5),
            RegistrationEnd = DateTime.UtcNow.AddDays(5),
            AddDropStart = DateTime.UtcNow.AddDays(6),
            AddDropEnd = DateTime.UtcNow.AddDays(12),
            WithdrawalDeadline = DateTime.UtcNow.AddDays(49),
            TermStart = DateTime.UtcNow.AddDays(-10),
            TermEnd = DateTime.UtcNow.AddDays(90),
            IsActive = true,
            Status = TermStatus.RegistrationOpen
        });

        // Student (active, good CGPA, no financial block)
        _context.Students.Add(new Student
        {
            Id = StudentId,
            UserId = 100,
            StudentNumber = "20220001",
            NationalId = "12345678901",
            Status = StudentStatus.Active,
            Cgpa = 3.00m,
            Sgpa = 3.00m,
            CompletedSemesters = 4,
            FrozenSemesterCount = 0,
            WithdrawalCount = 0,
            IsScholarshipStudent = false,
            HasFinancialBlock = false,
            AdvisorId = null
        });

        // StudentProgram linking student to program/curriculum
        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = StudentId,
            ProgramId = ProgramId,
            CurriculumId = CurriculumId,
            Type = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow.AddYears(-2),
            IsActive = true
        });

        _context.SaveChanges();
    }

    private static ISystemConfigService GetSystemConfig()
    {
        var mock = new Mock<ISystemConfigService>();
        mock.Setup(x => x.GetIntAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync((string _, int def) => def);
        mock.Setup(x => x.GetDecimalAsync(It.IsAny<string>(), It.IsAny<decimal>())).ReturnsAsync((string _, decimal def) => def);
        mock.Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((string _, string def) => def);
        return mock.Object;
    }

    // ──────────────────────────── Helpers ────────────────────────────

    private Course AddCourse(int id, string code, string name, int ects, int? departmentId = null)
    {
        departmentId ??= DepartmentId;
        var course = new Course
        {
            Id = id,
            Code = code,
            Name = name,
            Ects = ects,
            DepartmentId = departmentId,
            TheoryHours = 3,
            LabHours = 0,
            IsNonGpa = false
        };
        _context.Courses.Add(course);
        _context.SaveChanges();
        return course;
    }

    private CourseSection AddSection(int id, int courseId, int termId, int capacity = 40,
        int enrolledCount = 0, string? scheduleJson = null, bool isActive = true)
    {
        var section = new CourseSection
        {
            Id = id,
            CourseId = courseId,
            TermId = termId,
            Capacity = capacity,
            EnrolledCount = enrolledCount,
            IsActive = isActive,
            ScheduleJson = scheduleJson ?? "[]"
        };
        _context.CourseSections.Add(section);
        _context.SaveChanges();
        return section;
    }

    private Domain.Entities.CourseRegistration AddRegistration(int id, int studentId, int sectionId,
        RegistrationStatus status, GradeCode? grade = null)
    {
        var reg = new Domain.Entities.CourseRegistration
        {
            Id = id,
            StudentId = studentId,
            CourseSectionId = sectionId,
            Status = status,
            Grade = grade,
            CreatedAt = DateTime.UtcNow
        };
        _context.CourseRegistrations.Add(reg);
        _context.SaveChanges();
        return reg;
    }

    private void SetStudentStatus(StudentStatus status)
    {
        var student = _context.Students.Find(StudentId)!;
        student.Status = status;
        _context.SaveChanges();
    }

    private void SetStudentCgpa(decimal cgpa)
    {
        var student = _context.Students.Find(StudentId)!;
        student.Cgpa = cgpa;
        _context.SaveChanges();
    }

    private void SetStudentFinancialBlock(bool hasBlock)
    {
        var student = _context.Students.Find(StudentId)!;
        student.HasFinancialBlock = hasBlock;
        _context.SaveChanges();
    }

    private void SetStudentCompletedSemesters(int semesters)
    {
        var student = _context.Students.Find(StudentId)!;
        student.CompletedSemesters = semesters;
        _context.SaveChanges();
    }

    private static string MakeScheduleJson(DayOfWeek day, TimeOnly start, TimeOnly end)
    {
        return JsonSerializer.Serialize(new List<ScheduleSlot>
        {
            new() { Day = day, StartTime = start, EndTime = end }
        });
    }

    // ═════════════════════════════════════════════════════════════════
    //  CHECK 1: Student Status
    // ═════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(StudentStatus.Passive)]
    [InlineData(StudentStatus.Frozen)]
    [InlineData(StudentStatus.Suspended)]
    [InlineData(StudentStatus.Dismissed)]
    [InlineData(StudentStatus.Graduated)]
    public async Task Validate_InactiveStudent_ReturnsStudentStatusError(StudentStatus status)
    {
        SetStudentStatus(status);
        var course = AddCourse(100, "CS100", "Intro", 5);
        var section = AddSection(100, course.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "StudentStatus");
    }

    [Fact]
    public async Task Validate_ActiveStudent_NoStudentStatusError()
    {
        // Student is already Active from seed
        var course = AddCourse(101, "CS101", "Intro", 5);
        var section = AddSection(101, course.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.Errors.Should().NotContain(e => e.Rule == "StudentStatus");
    }

    // ═════════════════════════════════════════════════════════════════
    //  CHECK 2: Financial Eligibility
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Validate_StudentWithFinancialBlock_ReturnsFinancialError()
    {
        SetStudentFinancialBlock(true);
        var course = AddCourse(200, "CS200", "Data Structures", 5);
        var section = AddSection(200, course.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "FinancialEligibility");
    }

    [Fact]
    public async Task Validate_StudentWithoutFinancialBlock_NoFinancialError()
    {
        // HasFinancialBlock is false by default
        var course = AddCourse(201, "CS201", "Algorithms", 5);
        var section = AddSection(201, course.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.Errors.Should().NotContain(e => e.Rule == "FinancialEligibility");
    }

    // ═════════════════════════════════════════════════════════════════
    //  CHECK 3: Registration Period
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Validate_OutsideRegistrationPeriod_ReturnsPeriodError()
    {
        // Set term status to Published (not RegistrationOpen/AddDrop)
        var term = _context.AcademicTerms.Find(TermId)!;
        term.Status = TermStatus.Published;
        _context.SaveChanges();

        var course = AddCourse(300, "CS300", "Operating Systems", 5);
        var section = AddSection(300, course.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "RegistrationPeriod");
    }

    [Fact]
    public async Task Validate_NoActiveTerm_ReturnsPeriodError()
    {
        // Deactivate the existing term
        var term = _context.AcademicTerms.Find(TermId)!;
        term.IsActive = false;
        _context.SaveChanges();

        var course = AddCourse(301, "CS301", "Networks", 5);
        var section = AddSection(301, course.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "RegistrationPeriod");
    }

    [Fact]
    public async Task Validate_InsideRegistrationPeriod_NoPeriodError()
    {
        // Term already has valid registration window from seed
        var course = AddCourse(302, "CS302", "Databases", 5);
        var section = AddSection(302, course.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.Errors.Should().NotContain(e => e.Rule == "RegistrationPeriod");
    }

    // ═════════════════════════════════════════════════════════════════
    //  CHECK 4: Prerequisite
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Validate_PrerequisiteNotMet_ReturnsPrerequisiteError()
    {
        var prereqCourse = AddCourse(400, "CS400", "Calculus I", 5);
        var mainCourse = AddCourse(401, "CS401", "Calculus II", 5);

        // Add prerequisite relationship
        _context.CoursePrerequisites.Add(new CoursePrerequisite
        {
            CourseId = mainCourse.Id,
            RequiredCourseId = prereqCourse.Id
        });
        _context.SaveChanges();

        var section = AddSection(401, mainCourse.Id, TermId);

        // Student has NOT taken the prerequisite at all
        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "Prerequisite");
    }

    [Fact]
    public async Task Validate_PrerequisiteMet_NoPrerequisiteError()
    {
        var prereqCourse = AddCourse(402, "CS402", "Physics I", 5);
        var mainCourse = AddCourse(403, "CS403", "Physics II", 5);

        _context.CoursePrerequisites.Add(new CoursePrerequisite
        {
            CourseId = mainCourse.Id,
            RequiredCourseId = prereqCourse.Id
        });
        _context.SaveChanges();

        // Student passed the prerequisite (grade BB)
        var prereqSection = AddSection(402, prereqCourse.Id, TermId);
        AddRegistration(402, StudentId, prereqSection.Id, RegistrationStatus.Active, GradeCode.BB);

        var mainSection = AddSection(403, mainCourse.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { mainSection.Id });

        result.Errors.Should().NotContain(e => e.Rule == "Prerequisite");
    }

    [Fact]
    public async Task Validate_PrerequisiteMetViaEquivalency_NoError()
    {
        var prereqCourse = AddCourse(404, "CS404", "Math I", 5);
        var equivalentCourse = AddCourse(405, "MATH100", "Mathematics I", 5);
        var mainCourse = AddCourse(406, "CS406", "Math II", 5);

        // mainCourse requires prereqCourse
        _context.CoursePrerequisites.Add(new CoursePrerequisite
        {
            CourseId = mainCourse.Id,
            RequiredCourseId = prereqCourse.Id
        });

        // prereqCourse and equivalentCourse are equivalent
        _context.CourseEquivalencies.Add(new CourseEquivalency
        {
            CourseId = prereqCourse.Id,
            EquivalentCourseId = equivalentCourse.Id
        });
        _context.SaveChanges();

        // Student passed the EQUIVALENT course, not the actual prerequisite
        var eqSection = AddSection(405, equivalentCourse.Id, TermId);
        AddRegistration(405, StudentId, eqSection.Id, RegistrationStatus.Active, GradeCode.CC);

        var mainSection = AddSection(406, mainCourse.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { mainSection.Id });

        result.Errors.Should().NotContain(e => e.Rule == "Prerequisite");
    }

    [Fact]
    public async Task Validate_PrerequisiteWithWGrade_ReturnsPrerequisiteError()
    {
        var prereqCourse = AddCourse(407, "CS407", "Stats I", 5);
        var mainCourse = AddCourse(408, "CS408", "Stats II", 5);

        _context.CoursePrerequisites.Add(new CoursePrerequisite
        {
            CourseId = mainCourse.Id,
            RequiredCourseId = prereqCourse.Id
        });
        _context.SaveChanges();

        // Student has W (withdrawal) grade - should NOT count as passing
        var prereqSection = AddSection(407, prereqCourse.Id, TermId);
        AddRegistration(407, StudentId, prereqSection.Id, RegistrationStatus.Withdrawn, GradeCode.W);

        var mainSection = AddSection(408, mainCourse.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { mainSection.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "Prerequisite");
    }

    [Fact]
    public async Task Validate_PrerequisiteWithIGrade_ReturnsPrerequisiteError()
    {
        var prereqCourse = AddCourse(409, "CS409", "Logic I", 5);
        var mainCourse = AddCourse(410, "CS410", "Logic II", 5);

        _context.CoursePrerequisites.Add(new CoursePrerequisite
        {
            CourseId = mainCourse.Id,
            RequiredCourseId = prereqCourse.Id
        });
        _context.SaveChanges();

        // Student has I (incomplete) grade - should NOT count as passing
        var prereqSection = AddSection(409, prereqCourse.Id, TermId);
        AddRegistration(409, StudentId, prereqSection.Id, RegistrationStatus.Active, GradeCode.I);

        var mainSection = AddSection(410, mainCourse.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { mainSection.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "Prerequisite");
    }

    // ═════════════════════════════════════════════════════════════════
    //  CHECK 5: Failed Course Priority
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Validate_FailedCourseNotSelected_ReturnsPriorityError()
    {
        // Student has failed a course
        var failedCourse = AddCourse(500, "CS500", "Failed Course", 5);
        var failedSection = AddSection(500, failedCourse.Id, TermId, isActive: true);
        AddRegistration(500, StudentId, failedSection.Id, RegistrationStatus.Active, GradeCode.FF);

        // Another section of the same failed course is available in the active term
        var failedCourseNewSection = AddSection(501, failedCourse.Id, TermId, isActive: true);

        // Student tries to register for a DIFFERENT course but NOT the failed one
        var otherCourse = AddCourse(502, "CS502", "Other Course", 5);
        var otherSection = AddSection(502, otherCourse.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { otherSection.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "FailedCoursePriority");
    }

    [Fact]
    public async Task Validate_FailedCourseSelected_NoPriorityError()
    {
        var failedCourse = AddCourse(503, "CS503", "Failed Course 2", 5);
        var failedSection = AddSection(503, failedCourse.Id, TermId, isActive: true);
        AddRegistration(503, StudentId, failedSection.Id, RegistrationStatus.Active, GradeCode.FF);

        // New section of the failed course available
        var failedCourseNewSection = AddSection(504, failedCourse.Id, TermId, isActive: true);

        // Student selects the failed course's new section
        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { failedCourseNewSection.Id });

        result.Errors.Should().NotContain(e => e.Rule == "FailedCoursePriority");
    }

    [Fact]
    public async Task Validate_FailedCourseNotAvailableThisTerm_NoPriorityError()
    {
        var failedCourse = AddCourse(505, "CS505", "Unavailable Failed", 5);
        // Past section with FF grade
        var pastSection = AddSection(505, failedCourse.Id, TermId, isActive: false); // not active
        AddRegistration(505, StudentId, pastSection.Id, RegistrationStatus.Active, GradeCode.FF);

        // No active section available for the failed course in the active term
        // Student registers for something else
        var otherCourse = AddCourse(506, "CS506", "Another Course", 5);
        var otherSection = AddSection(506, otherCourse.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { otherSection.Id });

        // Since the failed course has no active section this term, no priority error
        result.Errors.Should().NotContain(e => e.Rule == "FailedCoursePriority");
    }

    // ═════════════════════════════════════════════════════════════════
    //  CHECK 6: ECTS Cap
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Validate_ECTSExceedsLimit_CgpaBelow2_Returns30ECTSError()
    {
        SetStudentCgpa(1.50m);

        // Student wants to register for 35 ECTS (exceeds 30 limit)
        var course1 = AddCourse(600, "CS600", "Course A", 20);
        var course2 = AddCourse(601, "CS601", "Course B", 15);
        var section1 = AddSection(600, course1.Id, TermId);
        var section2 = AddSection(601, course2.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section1.Id, section2.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "ECTSCap");
    }

    [Fact]
    public async Task Validate_ECTSWithin30Limit_CgpaBelow2_NoError()
    {
        SetStudentCgpa(1.50m);

        // 30 ECTS exactly, within limit
        var course = AddCourse(602, "CS602", "Course C", 30);
        var section = AddSection(602, course.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.Errors.Should().NotContain(e => e.Rule == "ECTSCap");
    }

    [Fact]
    public async Task Validate_ECTSWithin42Limit_Cgpa2to249_NoError()
    {
        SetStudentCgpa(2.20m);

        // 42 ECTS exactly, within limit for 2.00-2.49 range
        var course = AddCourse(603, "CS603", "Course D", 42);
        var section = AddSection(603, course.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.Errors.Should().NotContain(e => e.Rule == "ECTSCap");
    }

    [Fact]
    public async Task Validate_ECTSExceeds42Limit_Cgpa2to249_ReturnsECTSError()
    {
        SetStudentCgpa(2.20m);

        var course = AddCourse(604, "CS604", "Course E", 43);
        var section = AddSection(604, course.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "ECTSCap");
    }

    [Fact]
    public async Task Validate_ECTSWithin48Limit_CgpaAbove250_NoError()
    {
        SetStudentCgpa(3.00m);

        // 48 ECTS exactly, within limit for >= 2.50
        var course = AddCourse(605, "CS605", "Course F", 48);
        var section = AddSection(605, course.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.Errors.Should().NotContain(e => e.Rule == "ECTSCap");
    }

    [Fact]
    public async Task Validate_ECTSExceeds48Limit_CgpaAbove250_ReturnsECTSError()
    {
        SetStudentCgpa(3.50m);

        var course = AddCourse(606, "CS606", "Course G", 49);
        var section = AddSection(606, course.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "ECTSCap");
    }

    [Fact]
    public async Task Validate_ECTSIncludesExistingRegistrations()
    {
        SetStudentCgpa(1.50m); // 30 ECTS limit

        // Student already has 25 ECTS registered
        var existingCourse = AddCourse(607, "CS607", "Existing", 25);
        var existingSection = AddSection(607, existingCourse.Id, TermId);
        AddRegistration(607, StudentId, existingSection.Id, RegistrationStatus.Active);

        // Student wants to add 10 more ECTS (total = 35, exceeds 30)
        var newCourse = AddCourse(608, "CS608", "New Course", 10);
        var newSection = AddSection(608, newCourse.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { newSection.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "ECTSCap");
    }

    // ═════════════════════════════════════════════════════════════════
    //  CHECK 7: CGPA Restriction (Retained Student)
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Validate_RetainedStudent_CannotTakeUpperSemesterCourse()
    {
        SetStudentCgpa(1.50m); // < 1.70
        SetStudentCompletedSemesters(4);

        // A course in semester 6 (upper than student's 4)
        var upperCourse = AddCourse(700, "CS700", "Advanced AI", 5);
        var section = AddSection(700, upperCourse.Id, TermId);

        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId,
            CourseId = upperCourse.Id,
            IsCompulsory = true,
            SuggestedSemester = 6
        });
        _context.SaveChanges();

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "CGPARestriction" && e.CourseSectionId == section.Id);
    }

    [Fact]
    public async Task Validate_RetainedStudent_CanTakeFailedCourse()
    {
        SetStudentCgpa(1.50m); // < 1.70
        SetStudentCompletedSemesters(4);

        var failedCourse = AddCourse(701, "CS701", "Failed Upper", 5);

        // The course is in semester 6 (upper) BUT student failed it
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId,
            CourseId = failedCourse.Id,
            IsCompulsory = true,
            SuggestedSemester = 6
        });
        _context.SaveChanges();

        // Student failed this course
        var pastSection = AddSection(701, failedCourse.Id, TermId);
        AddRegistration(701, StudentId, pastSection.Id, RegistrationStatus.Active, GradeCode.FF);

        // Available section for the same failed course
        var newSection = AddSection(702, failedCourse.Id, TermId, isActive: true);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { newSection.Id });

        result.Errors.Should().NotContain(e => e.Rule == "CGPARestriction");
    }

    [Fact]
    public async Task Validate_RetainedStudent_CanTakeDDDCImprovement()
    {
        SetStudentCgpa(1.50m);
        SetStudentCompletedSemesters(4);

        var ddCourse = AddCourse(703, "CS703", "DD Course", 5);
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId,
            CourseId = ddCourse.Id,
            IsCompulsory = true,
            SuggestedSemester = 6 // upper semester
        });
        _context.SaveChanges();

        // Student got DD grade in this course
        var pastSection = AddSection(703, ddCourse.Id, TermId);
        AddRegistration(703, StudentId, pastSection.Id, RegistrationStatus.Active, GradeCode.DD);

        var newSection = AddSection(704, ddCourse.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { newSection.Id });

        result.Errors.Should().NotContain(e => e.Rule == "CGPARestriction");
    }

    [Fact]
    public async Task Validate_RetainedStudent_CanTakeLowerSemesterCourse()
    {
        SetStudentCgpa(1.50m);
        SetStudentCompletedSemesters(4);

        var lowerCourse = AddCourse(705, "CS705", "Lower Semester", 5);
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId,
            CourseId = lowerCourse.Id,
            IsCompulsory = true,
            SuggestedSemester = 3 // lower than completedSemesters=4
        });
        _context.SaveChanges();

        var section = AddSection(705, lowerCourse.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.Errors.Should().NotContain(e => e.Rule == "CGPARestriction");
    }

    [Fact]
    public async Task Validate_RetainedStudent_CanTakeSameSemesterCourse()
    {
        SetStudentCgpa(1.50m);
        SetStudentCompletedSemesters(4);

        var sameCourse = AddCourse(706, "CS706", "Same Semester", 5);
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId,
            CourseId = sameCourse.Id,
            IsCompulsory = true,
            SuggestedSemester = 4 // same as completedSemesters
        });
        _context.SaveChanges();

        var section = AddSection(706, sameCourse.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.Errors.Should().NotContain(e => e.Rule == "CGPARestriction");
    }

    [Fact]
    public async Task Validate_RetainedStudent_CanTakeNonDepartmentalCourse()
    {
        SetStudentCgpa(1.50m);
        SetStudentCompletedSemesters(4);

        // Course from a DIFFERENT department
        var nonDeptCourse = AddCourse(707, "EE707", "Non-Dept Course", 5, OtherDepartmentId);
        // Not in curriculum at all (or in upper semester but different department)
        var section = AddSection(707, nonDeptCourse.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.Errors.Should().NotContain(e => e.Rule == "CGPARestriction");
    }

    [Fact]
    public async Task Validate_NonRetainedStudent_NoRestriction()
    {
        // CGPA >= 1.70, so not retained
        SetStudentCgpa(2.50m);
        SetStudentCompletedSemesters(4);

        var upperCourse = AddCourse(708, "CS708", "Upper Course OK", 5);
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId,
            CourseId = upperCourse.Id,
            IsCompulsory = true,
            SuggestedSemester = 6
        });
        _context.SaveChanges();

        var section = AddSection(708, upperCourse.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.Errors.Should().NotContain(e => e.Rule == "CGPARestriction");
    }

    // ═════════════════════════════════════════════════════════════════
    //  CHECK 8: Schedule Conflict
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Validate_FullOverlap_FirstTimeCourse_ReturnsBlockingError()
    {
        var schedule = MakeScheduleJson(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0));

        // Existing registration
        var existingCourse = AddCourse(800, "CS800", "Existing", 5);
        var existingSection = AddSection(800, existingCourse.Id, TermId, scheduleJson: schedule);
        AddRegistration(800, StudentId, existingSection.Id, RegistrationStatus.Active);

        // New course with exact same schedule (full overlap)
        var newCourse = AddCourse(801, "CS801", "New Overlap", 5);
        var newSection = AddSection(801, newCourse.Id, TermId, scheduleJson: schedule);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { newSection.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "ScheduleConflict_FullBlock");
    }

    [Fact]
    public async Task Validate_FullOverlap_RepeatCourse_ReturnsApprovalNeeded_NotBlocking()
    {
        var schedule = MakeScheduleJson(DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(12, 0));

        // Existing registration for a different course in the current term
        var existingCourse = AddCourse(802, "CS802", "Existing2", 5);
        var existingSection = AddSection(802, existingCourse.Id, TermId, scheduleJson: schedule);
        AddRegistration(802, StudentId, existingSection.Id, RegistrationStatus.Active);

        // Create a past term for the failed registration
        var pastTermId = 50;
        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id = pastTermId,
            Name = "2024-2025 Fall",
            Year = 2024,
            Semester = SemesterType.Fall,
            RegistrationStart = DateTime.UtcNow.AddDays(-200),
            RegistrationEnd = DateTime.UtcNow.AddDays(-190),
            AddDropStart = DateTime.UtcNow.AddDays(-189),
            AddDropEnd = DateTime.UtcNow.AddDays(-183),
            WithdrawalDeadline = DateTime.UtcNow.AddDays(49),
            TermStart = DateTime.UtcNow.AddDays(-210),
            TermEnd = DateTime.UtcNow.AddDays(-120),
            IsActive = false
        });
        _context.SaveChanges();

        // Repeat course: student FAILED it in a PAST term
        var repeatCourse = AddCourse(803, "CS803", "Repeat", 5);
        var pastSection = AddSection(803, repeatCourse.Id, pastTermId);
        AddRegistration(803, StudentId, pastSection.Id, RegistrationStatus.Active, GradeCode.FF);

        // New section of the repeat course in current term with same schedule as existing
        var newSection = AddSection(804, repeatCourse.Id, TermId, scheduleJson: schedule);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { newSection.Id });

        // Should have NeedsApproval error, but IsValid should be TRUE
        result.IsValid.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Rule == "ScheduleConflict_NeedsApproval");
        result.Errors.Should().NotContain(e => e.Rule == "ScheduleConflict_FullBlock");
    }

    [Fact]
    public async Task Validate_PartialOverlap_ReturnsApprovalNeeded_NotBlocking()
    {
        var existingSchedule = MakeScheduleJson(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(11, 0));
        // New schedule overlaps partially: 10:00-12:00 overlaps with 9:00-11:00
        var newSchedule = MakeScheduleJson(DayOfWeek.Wednesday, new TimeOnly(10, 0), new TimeOnly(12, 0));

        var existingCourse = AddCourse(805, "CS805", "Existing3", 5);
        var existingSection = AddSection(805, existingCourse.Id, TermId, scheduleJson: existingSchedule);
        AddRegistration(805, StudentId, existingSection.Id, RegistrationStatus.Active);

        var newCourse = AddCourse(806, "CS806", "Partial Overlap", 5);
        var newSection = AddSection(806, newCourse.Id, TermId, scheduleJson: newSchedule);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { newSection.Id });

        // Partial overlap => NeedsApproval, not blocking
        result.IsValid.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Rule == "ScheduleConflict_NeedsApproval");
        result.Errors.Should().NotContain(e => e.Rule == "ScheduleConflict_FullBlock");
    }

    [Fact]
    public async Task Validate_NoScheduleOverlap_NoConflictError()
    {
        var schedule1 = MakeScheduleJson(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0));
        var schedule2 = MakeScheduleJson(DayOfWeek.Monday, new TimeOnly(13, 0), new TimeOnly(15, 0));

        var existingCourse = AddCourse(807, "CS807", "Morning", 5);
        var existingSection = AddSection(807, existingCourse.Id, TermId, scheduleJson: schedule1);
        AddRegistration(807, StudentId, existingSection.Id, RegistrationStatus.Active);

        var newCourse = AddCourse(808, "CS808", "Afternoon", 5);
        var newSection = AddSection(808, newCourse.Id, TermId, scheduleJson: schedule2);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { newSection.Id });

        result.Errors.Should().NotContain(e => e.Rule == "ScheduleConflict_FullBlock");
        result.Errors.Should().NotContain(e => e.Rule == "ScheduleConflict_NeedsApproval");
    }

    [Fact]
    public async Task Validate_ScheduleConflictBetweenNewSections_ReturnsBlockingError()
    {
        var schedule = MakeScheduleJson(DayOfWeek.Thursday, new TimeOnly(14, 0), new TimeOnly(16, 0));

        // Two NEW sections with the same schedule
        var course1 = AddCourse(809, "CS809", "NewA", 5);
        var section1 = AddSection(809, course1.Id, TermId, scheduleJson: schedule);

        var course2 = AddCourse(810, "CS810", "NewB", 5);
        var section2 = AddSection(810, course2.Id, TermId, scheduleJson: schedule);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section1.Id, section2.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "ScheduleConflict_FullBlock");
    }

    // ═════════════════════════════════════════════════════════════════
    //  CHECK 9: Capacity
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Validate_FullCapacity_ReturnsCapacityError()
    {
        var course = AddCourse(900, "CS900", "Full Class", 5);
        var section = AddSection(900, course.Id, TermId, capacity: 30, enrolledCount: 30);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "Capacity" && e.CourseSectionId == section.Id);
    }

    [Fact]
    public async Task Validate_OverCapacity_ReturnsCapacityError()
    {
        var course = AddCourse(901, "CS901", "Over Capacity", 5);
        var section = AddSection(901, course.Id, TermId, capacity: 30, enrolledCount: 35);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "Capacity");
    }

    [Fact]
    public async Task Validate_AvailableCapacity_NoCapacityError()
    {
        var course = AddCourse(902, "CS902", "Open Class", 5);
        var section = AddSection(902, course.Id, TermId, capacity: 40, enrolledCount: 20);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.Errors.Should().NotContain(e => e.Rule == "Capacity");
    }

    // ═════════════════════════════════════════════════════════════════
    //  CHECK 10: Duplicate Registration
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Validate_AlreadyRegistered_ReturnsDuplicateError()
    {
        var course = AddCourse(1000, "CS1000", "Dup Course", 5);
        var existingSection = AddSection(1000, course.Id, TermId);
        AddRegistration(1000, StudentId, existingSection.Id, RegistrationStatus.Active);

        // Try to register for a different section of the same course
        var newSection = AddSection(1001, course.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { newSection.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "DuplicateRegistration" && e.CourseSectionId == newSection.Id);
    }

    [Fact]
    public async Task Validate_NotYetRegistered_NoDuplicateError()
    {
        var course = AddCourse(1002, "CS1002", "New Course", 5);
        var section = AddSection(1002, course.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.Errors.Should().NotContain(e => e.Rule == "DuplicateRegistration");
    }

    [Fact]
    public async Task Validate_PendingApprovalRegistration_ReturnsDuplicateError()
    {
        var course = AddCourse(1003, "CS1003", "Pending Course", 5);
        var existingSection = AddSection(1003, course.Id, TermId);
        AddRegistration(1003, StudentId, existingSection.Id, RegistrationStatus.PendingAdvisorApproval);

        var newSection = AddSection(1004, course.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { newSection.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "DuplicateRegistration");
    }

    // ═════════════════════════════════════════════════════════════════
    //  Edge Cases / Integration
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Validate_StudentNotFound_ReturnsError()
    {
        var result = await _service.ValidateRegistrationAsync(99999, new List<int> { 1 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "StudentExists");
    }

    [Fact]
    public async Task Validate_ValidRegistration_ReturnsNoErrors()
    {
        // All conditions are met: active student, no financial block, within period, no prereq issues
        var course = AddCourse(1100, "CS1100", "Simple Course", 5);
        var section = AddSection(1100, course.Id, TermId);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_BlockingErrorsReturnEarly_SkipsPerSectionChecks()
    {
        // If student is inactive AND has financial block, we get early return
        // and no per-section errors (like Capacity, Prerequisite) should appear
        SetStudentStatus(StudentStatus.Frozen);
        SetStudentFinancialBlock(true);

        var course = AddCourse(1101, "CS1101", "Blocked Course", 5);
        var section = AddSection(1101, course.Id, TermId, capacity: 0, enrolledCount: 0);

        var result = await _service.ValidateRegistrationAsync(StudentId, new List<int> { section.Id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Rule == "StudentStatus");
        result.Errors.Should().Contain(e => e.Rule == "FinancialEligibility");
        // Capacity check should NOT have been reached
        result.Errors.Should().NotContain(e => e.Rule == "Capacity");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
