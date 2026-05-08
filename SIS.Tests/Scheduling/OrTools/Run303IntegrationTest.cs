using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.Common.Services;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Scheduling.OrTools;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Mapping;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools;

/// <summary>
/// Plan 2 Task 20 — Run 303 reproduction.
/// 4 CENG101 sections + 14 MUH classrooms + full week TimeSlots → assert v2 schedules all 4
/// (where v1 only managed 2/4 in production Run 303).
/// </summary>
public class Run303IntegrationTest
{
    [Fact]
    public async Task FourCeng101Sections_V2Engine_AllAssigned()
    {
        // Arrange
        await using var ctx = NewInMemoryContext();
        await SeedRun303ScenarioAsync(ctx);

        var configMock = BuildMockConfig();
        var requestBuilder = new SolverRequestBuilder(ctx, configMock.Object);
        var request = await requestBuilder.BuildAsync(termId: 2, facultyId: 1, departmentId: null, seed: 42, default);

        // Sanity check the seed worked
        request.Sections.Should().HaveCount(4, because: "4 CENG101 sections seeded");
        request.Classrooms.Should().HaveCount(14, because: "14 MUH classrooms seeded for Term 2");
        request.TimeSlots.Should().HaveCount(50, because: "50 weekday slots (5 days × 10/day)");

        var encoders = BuildAllHardEncoders();
        var modelBuilder = new ScheduleModelBuilder(encoders);
        var mapper = new SolutionMapper();
        var unsatInterpreter = new UnsatCoreInterpreter();
        var preflightChecker = new PreflightFeasibilityChecker();
        var engine = new CpSatSolverEngine(modelBuilder, mapper, configMock.Object, unsatInterpreter, preflightChecker);

        // Act
        var result = await engine.SolveAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().BeOneOf(
            new[] { SolverStatus.Optimal, SolverStatus.Feasible },
            because: "v2 engine should solve all 4 CENG101 sections (vs v1's 2/4 in Run 303)");
        result.Assignments.Should().HaveCount(4, because: "all 4 CENG101 sections must be assigned");
        result.Failures.Should().BeEmpty(because: "no section should fail with full classroom + slot grid");

        // Each assignment should have slots for both lecture (3) and lab (2)
        foreach (var assignment in result.Assignments)
        {
            var lectureSlots = assignment.Slots.Where(s => s.ComponentType == ComponentType.Lecture).ToList();
            var labSlots = assignment.Slots.Where(s => s.ComponentType == ComponentType.Lab).ToList();

            lectureSlots.Should().HaveCount(3, because: $"section {assignment.SectionId} has TheoryHours=3");
            labSlots.Should().HaveCount(2, because: $"section {assignment.SectionId} has LabHours=2");
        }
    }

    // ─────────────── Helpers ───────────────

    private static AppDbContext NewInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedRun303ScenarioAsync(AppDbContext ctx)
    {
        // Faculty + Department
        ctx.Faculties.Add(new Faculty { Id = 1, Code = "MUH", Name = "Mühendislik" });
        ctx.Departments.Add(new Department { Id = 1, Code = "BM", Name = "Bilgisayar", FacultyId = 1 });

        // Term: Bahar 2026
        ctx.AcademicTerms.Add(new AcademicTerm
        {
            Id = 2,
            Year = 2026,
            TermType = TermType.Spring,
            Name = "2025-2026 Bahar",
            ShortName = "B26",
            EnglishName = "Spring 2025-2026",
            ShortEnglishName = "S26",
            TermStart = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)
        });

        // Course: CENG101
        ctx.Courses.Add(new Course
        {
            Id = 100,
            Code = "CENG101",
            Name = "Algorithms and Programming with Java I",
            DepartmentId = 1,
            TheoryHours = 3,
            LabHours = 2,
            TeachingMethod = TeachingMethod.FaceToFace,
            Ects = 6,
            NationalCredit = 6
        });

        // 4 CourseSections
        for (int i = 1; i <= 4; i++)
        {
            ctx.CourseSections.Add(new CourseSection
            {
                Id = 1000 + i,
                CourseId = 100,
                TermId = 2,
                SectionNumber = i,
                Capacity = 40,
                Status = CourseSectionStatus.Active,
                IsFinalized = false
            });
        }

        // 14 Classrooms
        var classrooms = new List<(int id, string name, int cap, ClassroomType type)>
        {
            (1, "AMFİ 1", 110, ClassroomType.Amphitheater),
            (2, "Amfi - Sınıf-Yeni-Kat Sıfır", 80, ClassroomType.Amphitheater),
            (3, "C 017", 70, ClassroomType.Lecture),
            (4, "C 018", 70, ClassroomType.Lecture),
            (5, "C 025", 70, ClassroomType.Lecture),
            (6, "C 026", 70, ClassroomType.Lecture),
            (7, "C 036", 80, ClassroomType.Lecture),
            (8, "C 054", 30, ClassroomType.Lecture),
            (9, "C 005", 50, ClassroomType.Lecture),
            (10, "L139 Ceng Lab-1", 40, ClassroomType.Lab),
            (11, "L141 Ceng Lab-2", 40, ClassroomType.Lab),
            (12, "Ceng Lab-3 (Kat 0)", 40, ClassroomType.Lab),
            (13, "L140 EEE LAB", 40, ClassroomType.Lab),
            (14, "L143 PHY LAB", 40, ClassroomType.Lab)
        };
        foreach (var c in classrooms)
        {
            ctx.Classrooms.Add(new Classroom { Id = c.id, Name = c.name, Capacity = c.cap, Type = c.type });
            ctx.ClassroomTermAssignments.Add(new ClassroomTermAssignment { TermId = 2, FacultyId = 1, ClassroomId = c.id });
        }

        // TimeSlots: Mon-Fri, 10 slots/day = 50 total
        int slotId = 1;
        for (var day = DayOfWeek.Monday; day <= DayOfWeek.Friday; day++)
        {
            for (int s = 0; s < 10; s++)
            {
                var startHour = 9 + s;
                ctx.TimeSlots.Add(new TimeSlot
                {
                    Id = slotId++,
                    TermId = 2,
                    DayOfWeek = day,
                    SlotOrder = s + 1,
                    StartTime = new TimeOnly(startHour, 0),
                    EndTime = new TimeOnly(startHour, 50),
                    AllowedMode = TimeSlotAllowedMode.Both
                });
            }
        }

        // ConstraintDefinitions: all 12 hard enabled
        var hardCodes = new[] { "H1", "H3", "H4", "H5", "H6", "H8", "H10", "H11", "H12", "H13", "H16", "H17" };
        foreach (var code in hardCodes)
        {
            ctx.ConstraintDefinitions.Add(new ConstraintDefinition
            {
                Code = code,
                Name = code,
                IsEnabled = true,
                Category = "Hard"
            });
        }

        await ctx.SaveChangesAsync();
    }

    private static Mock<ISystemConfigProvider> BuildMockConfig()
    {
        var mock = new Mock<ISystemConfigProvider>();
        mock.Setup(x => x.GetIntAsync("Schedule.OrTools.TimeLimitSeconds", It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(60);
        mock.Setup(x => x.GetIntAsync("Schedule.OrTools.NumWorkers", It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(8);
        mock.Setup(x => x.GetIntAsync("Schedule.OrTools.RandomSeed", It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(42);
        mock.Setup(x => x.GetBoolAsync("Schedule.OrTools.LogToConsole", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        // Plan 5 Task 1: SolverRequestBuilder reads this; Task 4 H-WL encoder will use it as fallback when Instructor.MaxWeeklyHours == 0.
        mock.Setup(x => x.GetIntAsync("Schedule.DefaultInstructorMaxWeeklyHours", It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(30);
        return mock;
    }

    private static IConstraintEncoder[] BuildAllHardEncoders()
    {
        // All 12 hard encoders from Plan 2
        // H12 needs splitThreshold (use 4 default), H13 needs lunch times, H16 needs maxHours
        return new IConstraintEncoder[]
        {
            new ClassroomConflictEncoder(),
            new CapacityEncoder(),
            new ClassroomTypeEncoder(),
            new CurriculumConflictEncoder(),
            new TheoryLabComponentEncoder(),
            new SameCourseSectionEncoder(),
            new FacultyClassroomEncoder(),
            new CorequisiteEncoder(),
            new TheoryLabDaySplitEncoder(splitThreshold: 4),
            new LunchBreakEncoder(new TimeOnly(11, 40), new TimeOnly(12, 20)),
            new MaxConsecutiveHoursEncoder(maxHoursPerDay: 4),
            new FaceToFaceSlotEncoder()
        };
    }
}
