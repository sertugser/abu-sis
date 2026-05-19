using FluentAssertions;
using Moq;
using SIS.Application.Common.Services;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Mapping;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools;

/// <summary>
/// Plan 7b Task 9 — Combined integration test exercising all 4 Phase A/B/C features
/// simultaneously: Online section (no room), Capacity 120 -> Amphitheater required,
/// RequiredLabClassroomType=PhysicsLab, and LabHours=4 multi-interval split.
/// </summary>
public class Plan7bCombinedIntegrationTest
{
    [Fact]
    public async Task SolveAsync_AllFourFeaturesTogether_ProducesValidSchedule()
    {
        var sections = new[]
        {
            // 1) Online section — no room, room IntVar atlandı
            new SectionInput
            {
                Id = 1, CourseId = 101, CourseCode = "ONLINE", CourseName = "Online",
                SectionNumber = 1, Capacity = 50,
                TheoryHours = 2, LabHours = 0,
                TeachingMethod = TeachingMethod.Online
            },
            // 2) Capacity 120 -> Amphitheater required
            new SectionInput
            {
                Id = 2, CourseId = 102, CourseCode = "CENG101", CourseName = "BigLecture",
                SectionNumber = 1, Capacity = 120,
                TheoryHours = 2, LabHours = 0,
                TeachingMethod = TeachingMethod.FaceToFace
            },
            // 3) RequiredLabClassroomType = PhysicsLab
            new SectionInput
            {
                Id = 3, CourseId = 103, CourseCode = "PHYS201", CourseName = "PhysLab",
                SectionNumber = 1, Capacity = 24,
                TheoryHours = 0, LabHours = 2,
                TeachingMethod = TeachingMethod.FaceToFace,
                RequiredLabClassroomType = (int)ClassroomType.PhysicsLab
            },
            // 4) LabHours = 4 — multi-interval split
            new SectionInput
            {
                Id = 4, CourseId = 104, CourseCode = "CHEM201", CourseName = "Chem",
                SectionNumber = 1, Capacity = 20,
                TheoryHours = 0, LabHours = 4,
                TeachingMethod = TeachingMethod.FaceToFace
            }
        };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "Amfi", Capacity = 200, Type = ClassroomType.Amphitheater },
            new ClassroomInput { Id = 2, Name = "PL", Capacity = 30, Type = ClassroomType.PhysicsLab },
            new ClassroomInput { Id = 3, Name = "ChemLab", Capacity = 30, Type = ClassroomType.Lab }
        };
        var slots = BuildFiveDayFourSlotGrid();
        var request = new SolverRequest
        {
            TermId = 1, FacultyId = 1,
            Sections = sections, Classrooms = classrooms, TimeSlots = slots,
            ConstraintFlags = new Dictionary<string, bool>
                { ["H1"] = true, ["H4"] = true, ["H17"] = true },
            AmphiThreshold = 100,
            AmphiThresholdEnabled = true,             // 2026-05-19: H4.Parameters yerine direkt request
            ClassroomTypeRequirementEnabled = true,   // 2026-05-19
            MaxLabBlockHours = 2
        };

        var engine = BuildEngine(amphiThreshold: 100, maxBlockHours: 2);
        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(new[] { SolverStatus.Optimal, SolverStatus.Feasible });
        result.Assignments.Should().HaveCount(4);

        // Online — ClassroomId=0
        result.Assignments.Single(a => a.SectionId == 1).Slots
            .Should().AllSatisfy(s => s.ClassroomId.Should().Be(0));

        // Big lecture — Amphitheater (id=1)
        result.Assignments.Single(a => a.SectionId == 2).Slots
            .Select(s => s.ClassroomId).Distinct()
            .Should().BeEquivalentTo(new[] { 1 });

        // Physics lab — PhysicsLab (id=2)
        result.Assignments.Single(a => a.SectionId == 3).Slots
            .Select(s => s.ClassroomId).Distinct()
            .Should().BeEquivalentTo(new[] { 2 });

        // 4h lab split -> 2 days. Chem has no RequiredLabClassroomType, so encoder
        // fallback restricts to Lab|Computer types — only id=3 (Lab) qualifies.
        var chemAssignment = result.Assignments.Single(a => a.SectionId == 4);
        chemAssignment.Slots.Should().HaveCount(4);
        chemAssignment.Slots.Select(s => s.ClassroomId).Distinct()
            .Should().BeEquivalentTo(new[] { 3 },
                because: "no RequiredLabClassroomType -> Lab|Computer fallback; only Lab room is id=3");
        var chemDays = chemAssignment.Slots
            .Select(s => slots.First(t => t.Id == s.TimeSlotId).DayOfWeek)
            .Distinct().Count();
        chemDays.Should().Be(2);
    }

    private static IReadOnlyList<TimeSlotInput> BuildFiveDayFourSlotGrid()
    {
        var slots = new List<TimeSlotInput>();
        for (int d = 1; d <= 5; d++)
            for (int s = 0; s < 4; s++)
                slots.Add(new TimeSlotInput
                {
                    Id = (d - 1) * 4 + s + 1,
                    DayOfWeek = (DayOfWeek)d,
                    SlotOrder = s + 1,
                    StartTime = new TimeOnly(9 + s, 0),
                    EndTime = new TimeOnly(9 + s, 50),
                    AllowedMode = TimeSlotAllowedMode.Both
                });
        return slots;
    }

    private static CpSatSolverEngine BuildEngine(int amphiThreshold, int maxBlockHours)
    {
        var encoders = new IConstraintEncoder[]
        {
            new ClassroomConflictEncoder(),
            new ClassroomTypeEncoder(),
            new FaceToFaceSlotEncoder()
        };
        var modelBuilder = new ScheduleModelBuilder(encoders);
        var mapper = new SolutionMapper();
        var unsatInterpreter = new UnsatCoreInterpreter();
        var preflightChecker = new PreflightFeasibilityChecker();

        var configMock = new Mock<ISystemConfigProvider>();
        configMock.Setup(x => x.GetIntAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string k, int f, CancellationToken _) => k switch
            {
                "Classroom.AmphiThreshold" => amphiThreshold,
                "Schedule.MaxLabBlockHours" => maxBlockHours,
                _ => f
            });
        // Plan 9 Issue 1 — opt in to AmphiThreshold rule (this test asserts Capacity 120 → Amphitheater).
        // Plan 9 Issue 6 — opt in to ClassroomTypeRequirement rule (RequiredLabClassroomType=PhysicsLab).
        configMock.Setup(x => x.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string k, bool f, CancellationToken _) => k switch
            {
                "Schedule.AmphiThresholdEnabled" => true,
                "Schedule.ClassroomTypeRequirementEnabled" => true,
                _ => f
            });

        return new CpSatSolverEngine(modelBuilder, mapper, configMock.Object, unsatInterpreter, preflightChecker);
    }
}
