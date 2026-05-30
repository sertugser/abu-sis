using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.API.Controllers;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using System.Security.Claims;
using ApplicationUser = SIS.Infrastructure.Persistence.ApplicationUser;

namespace SIS.Tests.API;

public class SectionExamControllerTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<ISystemConfigProvider> _configMock;
    private readonly SectionExamController _controller;

    private const int InstructorUserId = 100;
    private const int InstructorId = 10;
    private const int SectionId = 1;
    private const int CourseId = 1;
    private const int TermId = 1;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;
    private const int MidtermComponentId = 1;

    public SectionExamControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        SeedBaseData();

        _configMock = new Mock<ISystemConfigProvider>();
        _configMock.Setup(c => c.GetIntAsync("Exam.DefaultDurationMinutes", 60, It.IsAny<CancellationToken>()))
            .ReturnsAsync(60);

        _controller = CreateController(InstructorUserId);
    }

    private SectionExamController CreateController(int userId)
    {
        var controller = new SectionExamController(_context, _configMock.Object);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private void SeedBaseData()
    {
        _context.Faculties.Add(new Faculty { Id = 1, Name = "F", Code = "F" });
        _context.Departments.Add(new Department { Id = 1, Name = "D", Code = "D", FacultyId = 1 });
        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId, Name = "P", Code = "P", DepartmentId = 1,
            DegreeLevel = DegreeLevel.Bachelor, EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 8, TotalRequiredEcts = 240
        });
        _context.Curricula.Add(new Curriculum { Id = CurriculumId, ProgramId = ProgramId, Name = "C2024", Version = "2024" });
        _context.Courses.Add(new Course { Id = CourseId, Code = "BM101", Name = "Course 1", DepartmentId = 1 });
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId, CourseId = CourseId, SuggestedSemester = 1
        });
        _context.Users.Add(new ApplicationUser
        {
            Id = InstructorUserId, UserName = "inst@test.edu",
            NormalizedUserName = "INST@TEST.EDU", Email = "inst@test.edu",
            NormalizedEmail = "INST@TEST.EDU", FirstName = "Test", LastName = "Inst"
        });
        _context.Instructors.Add(new Instructor
        {
            Id = InstructorId, UserId = InstructorUserId, DepartmentId = 1,
            Title = "Dr.", MaxWeeklyHours = 12
        });
        _context.CourseSections.Add(new CourseSection
        {
            Id = SectionId, CourseId = CourseId, TermId = TermId,
            InstructorId = InstructorId, SectionNumber = 1
        });
        _context.GradeComponents.Add(new GradeComponent
        {
            Id = MidtermComponentId, CourseId = CourseId, AcademicTermId = TermId,
            Name = "Vize", Type = GradeComponentType.Midterm, Weight = 40, Order = 1
        });
        _context.Classrooms.Add(new Classroom { Id = 1, Name = "A101", BuildingName = "A", Capacity = 50 });
        _context.Classrooms.Add(new Classroom { Id = 2, Name = "A102", BuildingName = "A", Capacity = 50 });
        _context.SaveChanges();
    }

    [Fact]
    public async Task List_ReturnsEmpty_WhenNoExams()
    {
        var result = await _controller.List(SectionId, default);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IEnumerable<SectionExamDto>>().Subject.Should().BeEmpty();
    }

    [Fact]
    public async Task List_ReturnsNotFound_WhenSectionNotOwned()
    {
        var other = CreateController(userId: 999);
        var result = await other.List(SectionId, default);
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    private static SaveSectionExamRequest ExamRequest(
        DateTime examDate, int componentId = MidtermComponentId, int? id = null,
        int durationMinutes = 60, params int[] classroomIds) =>
        new(id, componentId, examDate, durationMinutes, false, "Açıklama",
            classroomIds.Length == 0 ? new[] { 1 } : classroomIds);

    /// <summary>İkinci bir ders + şube + bileşen seed eder (kohort/oda çakışma testleri için).</summary>
    private void SeedSecondSection(int sectionId, int courseId, string code, int suggestedSemester,
        int componentId, int programId = ProgramId, int curriculumId = CurriculumId)
    {
        _context.Courses.Add(new Course { Id = courseId, Code = code, Name = code, DepartmentId = 1 });
        if (curriculumId != CurriculumId)
            _context.Curricula.Add(new Curriculum { Id = curriculumId, ProgramId = programId, Name = "C" + curriculumId });
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = curriculumId, CourseId = courseId, SuggestedSemester = suggestedSemester
        });
        _context.CourseSections.Add(new CourseSection
        {
            Id = sectionId, CourseId = courseId, TermId = TermId, InstructorId = InstructorId, SectionNumber = 1
        });
        _context.GradeComponents.Add(new GradeComponent
        {
            Id = componentId, CourseId = courseId, AcademicTermId = TermId,
            Name = "Vize", Type = GradeComponentType.Midterm, Weight = 40, Order = 1
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task Upsert_CreatesExam_WhenValid()
    {
        var result = await _controller.Upsert(SectionId,
            ExamRequest(new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc)), default);
        result.Should().BeOfType<OkObjectResult>();

        var items = ((IEnumerable<SectionExamDto>)((OkObjectResult)(await _controller.List(SectionId, default))).Value!).ToList();
        items.Should().ContainSingle().Which.Classrooms.Should().ContainSingle(c => c.ClassroomId == 1);
    }

    [Fact]
    public async Task Upsert_Rejects_WhenComponentNotInCourse()
    {
        var result = await _controller.Upsert(SectionId,
            ExamRequest(new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc), componentId: 999), default);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Upsert_Rejects_RoomConflict()
    {
        // Section 1: BM101 (1.sınıf), A101, 09:00
        await _controller.Upsert(SectionId,
            ExamRequest(new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc), classroomIds: new[] { 1 }), default);

        // Section 2: BM402 (4.sınıf — kohort çakışmaz), AMA aynı oda A101, çakışan saat → oda çakışması
        SeedSecondSection(sectionId: 2, courseId: 2, code: "BM402", suggestedSemester: 7, componentId: 2);
        var result = await _controller.Upsert(2,
            ExamRequest(new DateTime(2026, 1, 10, 9, 30, 0, DateTimeKind.Utc), componentId: 2, classroomIds: new[] { 1 }), default);
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Upsert_Rejects_CohortConflict_SameYear()
    {
        // Section 1: BM101 (1.sınıf), A101, 09:00
        await _controller.Upsert(SectionId,
            ExamRequest(new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc), classroomIds: new[] { 1 }), default);

        // Section 2: BM102 (1.sınıf — aynı kohort), FARKLI oda A102, çakışan saat → kohort çakışması
        SeedSecondSection(sectionId: 2, courseId: 2, code: "BM102", suggestedSemester: 1, componentId: 2);
        var result = await _controller.Upsert(2,
            ExamRequest(new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc), componentId: 2, classroomIds: new[] { 2 }), default);
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Upsert_Allows_NonAdjacentYear_DifferentRoom()
    {
        // Section 1: BM101 (1.sınıf), A101, 09:00
        await _controller.Upsert(SectionId,
            ExamRequest(new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc), classroomIds: new[] { 1 }), default);

        // Section 2: BM301 (3.sınıf — 1↔3 gap 2, komşu değil), farklı oda A102, çakışan saat → izin
        SeedSecondSection(sectionId: 2, courseId: 2, code: "BM301", suggestedSemester: 5, componentId: 2);
        var result = await _controller.Upsert(2,
            ExamRequest(new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc), componentId: 2, classroomIds: new[] { 2 }), default);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Upsert_UpdatesExisting_AndReplacesClassrooms()
    {
        var create = await _controller.Upsert(SectionId,
            ExamRequest(new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc), classroomIds: new[] { 1 }), default);
        var id = ((SectionExamDto)((OkObjectResult)create).Value!).Id;

        var result = await _controller.Upsert(SectionId,
            ExamRequest(new DateTime(2026, 1, 11, 14, 0, 0, DateTimeKind.Utc), id: id, classroomIds: new[] { 2 }), default);
        result.Should().BeOfType<OkObjectResult>();

        var items = ((IEnumerable<SectionExamDto>)((OkObjectResult)(await _controller.List(SectionId, default))).Value!).ToList();
        items.Should().ContainSingle();
        items[0].Classrooms.Should().ContainSingle(c => c.ClassroomId == 2);
        items[0].ExamDate.Should().Be(new DateTime(2026, 1, 11, 14, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Delete_RemovesExam_AndClassrooms()
    {
        var create = await _controller.Upsert(SectionId,
            ExamRequest(new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc), classroomIds: new[] { 1, 2 }), default);
        var id = ((SectionExamDto)((OkObjectResult)create).Value!).Id;

        var result = await _controller.Delete(SectionId, id, default);
        result.Should().BeOfType<NoContentResult>();

        (await _context.SectionExams.CountAsync()).Should().Be(0);
        (await _context.SectionExamClassrooms.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.Delete(SectionId, 9999, default);
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // Fix 1 — AnnouncedAt cleared when un-announcing
    [Fact]
    public async Task Upsert_ClearsAnnouncedAt_WhenUnAnnouncing()
    {
        // Create exam with IsAnnouncedToStudents = true
        var createReq = new SaveSectionExamRequest(
            null, MidtermComponentId,
            new DateTime(2026, 2, 10, 9, 0, 0, DateTimeKind.Utc),
            60, true, "Duyuruldu", new[] { 1 });
        var create = await _controller.Upsert(SectionId, createReq, default);
        var created = ((SectionExamDto)((OkObjectResult)create).Value!);
        created.IsAnnouncedToStudents.Should().BeTrue();
        created.AnnouncedAt.Should().NotBeNull();

        // Update same exam with IsAnnouncedToStudents = false
        var updateReq = new SaveSectionExamRequest(
            created.Id, MidtermComponentId,
            new DateTime(2026, 2, 10, 9, 0, 0, DateTimeKind.Utc),
            60, false, "Geri alındı", new[] { 1 });
        var update = await _controller.Upsert(SectionId, updateReq, default);
        update.Should().BeOfType<OkObjectResult>();

        var items = ((IEnumerable<SectionExamDto>)((OkObjectResult)(await _controller.List(SectionId, default))).Value!).ToList();
        items.Should().ContainSingle();
        items[0].IsAnnouncedToStudents.Should().BeFalse();
        items[0].AnnouncedAt.Should().BeNull();
    }

    // Fix 2 — 404 when updating non-existent exam id
    [Fact]
    public async Task Upsert_ReturnsNotFound_WhenUpdatingMissingExam()
    {
        var req = new SaveSectionExamRequest(
            9999, MidtermComponentId,
            new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc),
            60, false, null, new[] { 1 });
        var result = await _controller.Upsert(SectionId, req, default);
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // Fix 4 — input validation: default ExamDate rejected
    [Fact]
    public async Task Upsert_Rejects_WhenExamDateDefault()
    {
        var req = new SaveSectionExamRequest(
            null, MidtermComponentId,
            default, 60, false, null, new[] { 1 });
        var result = await _controller.Upsert(SectionId, req, default);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // Fix 4 — input validation: non-positive DurationMinutes rejected
    [Fact]
    public async Task Upsert_Rejects_WhenDurationNonPositive()
    {
        var req = new SaveSectionExamRequest(
            null, MidtermComponentId,
            new DateTime(2026, 4, 10, 9, 0, 0, DateTimeKind.Utc),
            0, false, null, new[] { 1 });
        var result = await _controller.Upsert(SectionId, req, default);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    public void Dispose() => _context.Dispose();
}
