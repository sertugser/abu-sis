using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Scheduling.OrTools;

namespace SIS.Tests.Scheduling.OrTools;

/// <summary>
/// SolverRequestBuilder — EF Core AppDbContext (InMemory) → SolverRequest DTO mapping testleri.
/// </summary>
public class SolverRequestBuilderTests
{
    private static AppDbContext NewInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// Plan 5 Task 1 — ISystemConfigProvider mock with configurable
    /// "Schedule.DefaultInstructorMaxWeeklyHours" return value. All other GetIntAsync
    /// calls fall back to the supplied fallback (passthrough).
    /// </summary>
    private static ISystemConfigProvider BuildConfigProviderMock(int defaultMaxWeekly = 30)
    {
        var mock = new Mock<ISystemConfigProvider>();
        mock.Setup(x => x.GetIntAsync(
                "Schedule.DefaultInstructorMaxWeeklyHours",
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultMaxWeekly);
        mock.Setup(x => x.GetIntAsync(
                It.Is<string>(k => k != "Schedule.DefaultInstructorMaxWeeklyHours"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, int fallback, CancellationToken _) => fallback);
        return mock.Object;
    }

    /// <summary>
    /// Helper: minimum geçerli senaryo seed eder — 1 faculty, 1 department, 1 term,
    /// 1 course, 1 active+non-finalized section, 1 classroom assignment, 1 timeslot,
    /// 2 constraint definition (1 enabled, 1 disabled).
    /// </summary>
    private static async Task<AppDbContext> SeedMinimalAsync()
    {
        var ctx = NewInMemoryContext();

        // Faculty + Department
        ctx.Faculties.Add(new Faculty { Id = 1, Code = "MUH", Name = "Mühendislik" });
        ctx.Departments.Add(new Department { Id = 1, Code = "BM", Name = "Bilgisayar", FacultyId = 1 });

        // Term
        ctx.AcademicTerms.Add(new AcademicTerm
        {
            Id = 1,
            Year = 2026,
            TermType = TermType.Spring,
            Name = "2025-2026 Bahar",
            ShortName = "B26",
            EnglishName = "Spring 2025-2026",
            ShortEnglishName = "S26",
            TermStart = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)
        });

        // Course
        ctx.Courses.Add(new Course
        {
            Id = 100,
            Code = "CENG101",
            Name = "Algorithms",
            DepartmentId = 1,
            TheoryHours = 3,
            LabHours = 2,
            TeachingMethod = TeachingMethod.FaceToFace,
            Ects = 6,
            NationalCredit = 4
        });

        // CourseSection — Active, not finalized (must be included)
        ctx.CourseSections.Add(new CourseSection
        {
            Id = 1000,
            CourseId = 100,
            TermId = 1,
            SectionNumber = 1,
            Capacity = 40,
            Status = CourseSectionStatus.Active,
            IsFinalized = false
        });

        // Classroom + faculty term assignment
        ctx.Classrooms.Add(new Classroom
        {
            Id = 9,
            Name = "C 005",
            BuildingName = "C Blok",
            Floor = 0,
            Capacity = 50,
            Type = ClassroomType.Lecture
        });
        ctx.ClassroomTermAssignments.Add(new ClassroomTermAssignment
        {
            Id = 1,
            TermId = 1,
            FacultyId = 1,
            ClassroomId = 9
        });

        // TimeSlot — term grid
        ctx.TimeSlots.Add(new TimeSlot
        {
            Id = 1,
            TermId = 1,
            DayOfWeek = DayOfWeek.Monday,
            SlotOrder = 1,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 50),
            DisplayName = "Pazartesi 1. Ders (09:00-09:50)",
            AllowedMode = TimeSlotAllowedMode.Both
        });

        // ConstraintDefinitions — flag mapping testi için 1 enabled + 1 disabled
        ctx.ConstraintDefinitions.Add(new ConstraintDefinition
        {
            Id = 1,
            Code = "H1",
            Name = "ClassroomConflict",
            Category = "Hard",
            IsEnabled = true
        });
        ctx.ConstraintDefinitions.Add(new ConstraintDefinition
        {
            Id = 2,
            Code = "H8",
            Name = "SameCourseSection",
            Category = "Hard",
            IsEnabled = false
        });

        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task BuildAsync_MinimalScenario_ReturnsCorrectIds()
    {
        // Arrange
        await using var ctx = await SeedMinimalAsync();
        var builder = new SolverRequestBuilder(ctx, BuildConfigProviderMock());

        // Act
        var req = await builder.BuildAsync(termId: 1, facultyId: 1, departmentId: null, seed: 42, default);

        // Assert
        req.TermId.Should().Be(1);
        req.FacultyId.Should().Be(1);
        req.DepartmentId.Should().BeNull();
        req.Seed.Should().Be(42);
    }

    [Fact]
    public async Task BuildAsync_MinimalScenario_PopulatesOneSection()
    {
        // Arrange
        await using var ctx = await SeedMinimalAsync();
        var builder = new SolverRequestBuilder(ctx, BuildConfigProviderMock());

        // Act
        var req = await builder.BuildAsync(1, 1, null, null, default);

        // Assert
        req.Sections.Should().HaveCount(1);
        var s = req.Sections[0];
        s.Id.Should().Be(1000);
        s.CourseId.Should().Be(100);
        s.CourseCode.Should().Be("CENG101");
        s.CourseName.Should().Be("Algorithms");
        s.SectionNumber.Should().Be(1);
        s.Capacity.Should().Be(40);
        s.TheoryHours.Should().Be(3);
        s.LabHours.Should().Be(2);
        s.TeachingMethod.Should().Be(TeachingMethod.FaceToFace);
        s.DepartmentId.Should().Be(1);
    }

    [Fact]
    public async Task BuildAsync_PopulatesClassroomsFromTermAssignments()
    {
        // Arrange
        await using var ctx = await SeedMinimalAsync();
        var builder = new SolverRequestBuilder(ctx, BuildConfigProviderMock());

        // Act
        var req = await builder.BuildAsync(1, 1, null, null, default);

        // Assert
        req.Classrooms.Should().HaveCount(1);
        var c = req.Classrooms[0];
        c.Id.Should().Be(9);
        c.Name.Should().Be("C 005");
        c.Capacity.Should().Be(50);
        c.Type.Should().Be(ClassroomType.Lecture);
    }

    [Fact]
    public async Task BuildAsync_PopulatesTimeSlots()
    {
        // Arrange
        await using var ctx = await SeedMinimalAsync();
        var builder = new SolverRequestBuilder(ctx, BuildConfigProviderMock());

        // Act
        var req = await builder.BuildAsync(1, 1, null, null, default);

        // Assert
        req.TimeSlots.Should().HaveCount(1);
        var ts = req.TimeSlots[0];
        ts.Id.Should().Be(1);
        ts.DayOfWeek.Should().Be(DayOfWeek.Monday);
        ts.SlotOrder.Should().Be(1);
        ts.StartTime.Should().Be(new TimeOnly(9, 0));
        ts.EndTime.Should().Be(new TimeOnly(9, 50));
        ts.AllowedMode.Should().Be(TimeSlotAllowedMode.Both);
    }

    [Fact]
    public async Task BuildAsync_PopulatesConstraintFlags()
    {
        // Arrange
        await using var ctx = await SeedMinimalAsync();
        var builder = new SolverRequestBuilder(ctx, BuildConfigProviderMock());

        // Act
        var req = await builder.BuildAsync(1, 1, null, null, default);

        // Assert
        req.ConstraintFlags.Should().ContainKey("H1").WhoseValue.Should().BeTrue();
        req.ConstraintFlags.Should().ContainKey("H8").WhoseValue.Should().BeFalse();
    }

    [Fact]
    public async Task BuildAsync_ExcludesCancelledSections()
    {
        // Arrange
        await using var ctx = await SeedMinimalAsync();
        ctx.CourseSections.Add(new CourseSection
        {
            Id = 1001,
            CourseId = 100,
            TermId = 1,
            SectionNumber = 2,
            Capacity = 40,
            Status = CourseSectionStatus.Cancelled,  // ← excluded
            IsFinalized = false
        });
        await ctx.SaveChangesAsync();
        var builder = new SolverRequestBuilder(ctx, BuildConfigProviderMock());

        // Act
        var req = await builder.BuildAsync(1, 1, null, null, default);

        // Assert
        req.Sections.Should().HaveCount(1);  // sadece Active section
        req.Sections.Should().NotContain(s => s.Id == 1001);
    }

    [Fact]
    public async Task BuildAsync_ExcludesFinalizedSections()
    {
        // Arrange
        await using var ctx = await SeedMinimalAsync();
        ctx.CourseSections.Add(new CourseSection
        {
            Id = 1002,
            CourseId = 100,
            TermId = 1,
            SectionNumber = 3,
            Capacity = 40,
            Status = CourseSectionStatus.Active,
            IsFinalized = true  // ← excluded
        });
        await ctx.SaveChangesAsync();
        var builder = new SolverRequestBuilder(ctx, BuildConfigProviderMock());

        // Act
        var req = await builder.BuildAsync(1, 1, null, null, default);

        // Assert
        req.Sections.Should().HaveCount(1);
        req.Sections.Should().NotContain(s => s.Id == 1002);
    }

    [Fact]
    public async Task BuildAsync_DepartmentFilter_OnlyIncludesMatchingSections()
    {
        // Arrange — aynı fakültede 2. department + course + section ekle
        await using var ctx = await SeedMinimalAsync();
        ctx.Departments.Add(new Department { Id = 2, Code = "EEE", Name = "Elektrik", FacultyId = 1 });
        ctx.Courses.Add(new Course
        {
            Id = 200,
            Code = "EEE101",
            Name = "Circuits",
            DepartmentId = 2,
            TheoryHours = 3,
            LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace,
            Ects = 6,
            NationalCredit = 3
        });
        ctx.CourseSections.Add(new CourseSection
        {
            Id = 1003,
            CourseId = 200,
            TermId = 1,
            SectionNumber = 1,
            Capacity = 40,
            Status = CourseSectionStatus.Active,
            IsFinalized = false
        });
        await ctx.SaveChangesAsync();
        var builder = new SolverRequestBuilder(ctx, BuildConfigProviderMock());

        // Act — sadece department 1 (BM)
        var reqDept1 = await builder.BuildAsync(1, 1, departmentId: 1, null, default);
        // Act — filtre yok, her ikisi
        var reqAll = await builder.BuildAsync(1, 1, departmentId: null, null, default);

        // Assert
        reqDept1.Sections.Should().HaveCount(1);
        reqDept1.Sections[0].CourseCode.Should().Be("CENG101");

        reqAll.Sections.Should().HaveCount(2);
    }

    [Fact]
    public async Task BuildAsync_EmptyDb_ReturnsEmptyCollections()
    {
        // Arrange
        await using var ctx = NewInMemoryContext();
        var builder = new SolverRequestBuilder(ctx, BuildConfigProviderMock());

        // Act
        var req = await builder.BuildAsync(termId: 1, facultyId: 1, null, null, default);

        // Assert
        req.Sections.Should().BeEmpty();
        req.Classrooms.Should().BeEmpty();
        req.TimeSlots.Should().BeEmpty();
        req.ConstraintFlags.Should().BeEmpty();
        req.Corequisites.Should().BeEmpty();
    }

    // ========================================================================
    // Plan 4 Task 1 — LabGroupCount calculation
    // ========================================================================

    /// <summary>
    /// Helper: paralel lab senaryosu seed eder — section capacity ve lab room
    /// capacity parametrize edilebilir. Course'un LabHours = 2 (lab component'li).
    /// Classroom Type = Lab (lab capacity hesabına dahil).
    /// </summary>
    private static async Task<AppDbContext> SeedScenarioWithCustomCapacityAsync(
        int sectionCapacity,
        int labRoomCapacity)
    {
        var ctx = NewInMemoryContext();

        ctx.Faculties.Add(new Faculty { Id = 1, Code = "MUH", Name = "Mühendislik" });
        ctx.Departments.Add(new Department { Id = 1, Code = "BM", Name = "Bilgisayar", FacultyId = 1 });
        ctx.AcademicTerms.Add(new AcademicTerm
        {
            Id = 1,
            Year = 2026,
            TermType = TermType.Spring,
            Name = "2025-2026 Bahar",
            ShortName = "B26",
            EnglishName = "Spring 2025-2026",
            ShortEnglishName = "S26",
            TermStart = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)
        });
        ctx.Courses.Add(new Course
        {
            Id = 100,
            Code = "X",
            Name = "X",
            DepartmentId = 1,
            TheoryHours = 3,
            LabHours = 2,
            TeachingMethod = TeachingMethod.FaceToFace,
            Ects = 6,
            NationalCredit = 6
        });
        ctx.CourseSections.Add(new CourseSection
        {
            Id = 1000,
            CourseId = 100,
            TermId = 1,
            SectionNumber = 1,
            Capacity = sectionCapacity,
            Status = CourseSectionStatus.Active,
            IsFinalized = false
        });
        ctx.Classrooms.Add(new Classroom
        {
            Id = 9,
            Name = "Lab",
            BuildingName = "C Blok",
            Floor = 0,
            Capacity = labRoomCapacity,
            Type = ClassroomType.Lab
        });
        ctx.ClassroomTermAssignments.Add(new ClassroomTermAssignment
        {
            Id = 1,
            TermId = 1,
            FacultyId = 1,
            ClassroomId = 9
        });

        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task BuildAsync_LabSectionFitsInSingleRoom_ReturnsLabGroupCount1()
    {
        // Arrange — section capacity 40, lab room cap 50 → groupCount = 1
        await using var ctx = await SeedScenarioWithCustomCapacityAsync(sectionCapacity: 40, labRoomCapacity: 50);
        var builder = new SolverRequestBuilder(ctx, BuildConfigProviderMock());

        // Act
        var req = await builder.BuildAsync(termId: 1, facultyId: 1, null, null, default);

        // Assert
        req.Sections.Should().HaveCount(1);
        req.Sections[0].LabGroupCount.Should().Be(1);
    }

    [Fact]
    public async Task BuildAsync_LabSectionExceedsSingleRoom_ReturnsLabGroupCount2()
    {
        // Arrange — section capacity 80, lab room cap 40 → groupCount = ⌈80/40⌉ = 2
        await using var ctx = await SeedScenarioWithCustomCapacityAsync(sectionCapacity: 80, labRoomCapacity: 40);
        var builder = new SolverRequestBuilder(ctx, BuildConfigProviderMock());

        // Act
        var req = await builder.BuildAsync(1, 1, null, null, default);

        // Assert
        req.Sections[0].LabGroupCount.Should().Be(2);
    }

    [Fact]
    public async Task BuildAsync_LabSection120StudentsLab40Cap_ReturnsLabGroupCount3()
    {
        // Arrange — 120 / 40 = 3 groups
        await using var ctx = await SeedScenarioWithCustomCapacityAsync(sectionCapacity: 120, labRoomCapacity: 40);
        var builder = new SolverRequestBuilder(ctx, BuildConfigProviderMock());

        // Act
        var req = await builder.BuildAsync(1, 1, null, null, default);

        // Assert
        req.Sections[0].LabGroupCount.Should().Be(3);
    }

    // ========================================================================
    // Plan 5 Task 1 — Instructor.MaxWeeklyHours override + SystemConfig default
    // ========================================================================

    /// <summary>
    /// Helper: section with an Instructor whose MaxWeeklyHours is configurable.
    /// </summary>
    private static async Task<AppDbContext> SeedScenarioWithInstructorMaxHoursAsync(int maxHours)
    {
        var ctx = NewInMemoryContext();
        ctx.Faculties.Add(new Faculty { Id = 1, Code = "MUH", Name = "M" });
        ctx.Departments.Add(new Department { Id = 1, Code = "BM", Name = "BM", FacultyId = 1 });
        ctx.AcademicTerms.Add(new AcademicTerm
        {
            Id = 1,
            Year = 2026,
            TermType = TermType.Spring,
            TermStart = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            ShortEnglishName = "S26"
        });
        ctx.Courses.Add(new Course
        {
            Id = 100,
            Code = "X",
            Name = "X",
            DepartmentId = 1,
            TheoryHours = 3,
            LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace,
            Ects = 6,
            NationalCredit = 6
        });
        // Instructor entity has UserId, DepartmentId, Title, MaxWeeklyHours (no Name field).
        ctx.Instructors.Add(new Instructor
        {
            Id = 7,
            UserId = 100,
            DepartmentId = 1,
            Title = "Prof.",
            MaxWeeklyHours = maxHours
        });
        ctx.CourseSections.Add(new CourseSection
        {
            Id = 1000,
            CourseId = 100,
            TermId = 1,
            SectionNumber = 1,
            Capacity = 30,
            InstructorId = 7,
            Status = CourseSectionStatus.Active,
            IsFinalized = false
        });
        ctx.Classrooms.Add(new Classroom
        {
            Id = 9,
            Name = "C",
            Capacity = 50,
            Type = ClassroomType.Lecture
        });
        ctx.ClassroomTermAssignments.Add(new ClassroomTermAssignment
        {
            Id = 1,
            TermId = 1,
            FacultyId = 1,
            ClassroomId = 9
        });
        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task BuildAsync_SectionWithInstructorHavingMaxHours_PopulatesOverride()
    {
        // Arrange
        await using var ctx = await SeedScenarioWithInstructorMaxHoursAsync(maxHours: 25);
        var builder = new SolverRequestBuilder(ctx, BuildConfigProviderMock(defaultMaxWeekly: 30));

        // Act
        var req = await builder.BuildAsync(termId: 1, facultyId: 1, null, null, default);

        // Assert
        req.Sections.Should().HaveCount(1);
        req.Sections[0].InstructorMaxWeeklyHours.Should().Be(25,
            because: "Instructor.MaxWeeklyHours override is loaded onto the SectionInput");
        req.DefaultInstructorMaxWeeklyHours.Should().Be(30,
            because: "SystemConfig provides the engine-wide default");
    }

    [Fact]
    public async Task BuildAsync_SectionWithInstructorMaxHoursZero_LeavesOverrideZero()
    {
        // Arrange — instructor exists but MaxWeeklyHours is 0 (sentinel for "use default")
        await using var ctx = await SeedScenarioWithInstructorMaxHoursAsync(maxHours: 0);
        var builder = new SolverRequestBuilder(ctx, BuildConfigProviderMock(defaultMaxWeekly: 30));

        // Act
        var req = await builder.BuildAsync(1, 1, null, null, default);

        // Assert
        req.Sections[0].InstructorMaxWeeklyHours.Should().Be(0,
            because: "0 means encoder falls back to DefaultInstructorMaxWeeklyHours");
    }

    [Fact]
    public async Task BuildAsync_NoInstructor_LeavesOverrideZero()
    {
        // Arrange — minimal seed has no instructor on the section
        await using var ctx = await SeedMinimalAsync();
        var builder = new SolverRequestBuilder(ctx, BuildConfigProviderMock(defaultMaxWeekly: 30));

        // Act
        var req = await builder.BuildAsync(1, 1, null, null, default);

        // Assert
        req.Sections.Should().NotBeEmpty();
        req.Sections[0].InstructorId.Should().BeNull();
        req.Sections[0].InstructorMaxWeeklyHours.Should().Be(0,
            because: "no instructor => no override; encoder uses default");
    }
}
