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

public class CpSatSolverEngineTests
{
    private static SectionInput Section(int id, int courseId, int theory, int lab) => new()
    {
        Id = id,
        CourseId = courseId,
        CourseCode = "X",
        CourseName = "X",
        SectionNumber = id,
        Capacity = 40,
        TheoryHours = theory,
        LabHours = lab,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    private static (CpSatSolverEngine engine, Mock<ISystemConfigProvider> configMock) BuildEngineWithConfig(
        int timeLimitSeconds = 30,
        int numWorkers = 8,
        int randomSeed = 42,
        bool logToConsole = false)
    {
        var configMock = new Mock<ISystemConfigProvider>();
        configMock.Setup(x => x.GetIntAsync("Schedule.OrTools.TimeLimitSeconds", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(timeLimitSeconds);
        configMock.Setup(x => x.GetIntAsync("Schedule.OrTools.NumWorkers", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(numWorkers);
        configMock.Setup(x => x.GetIntAsync("Schedule.OrTools.RandomSeed", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(randomSeed);
        configMock.Setup(x => x.GetBoolAsync("Schedule.OrTools.LogToConsole", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(logToConsole);

        var encoders = new IConstraintEncoder[] { new ClassroomConflictEncoder() };
        var modelBuilder = new ScheduleModelBuilder(encoders);
        var mapper = new SolutionMapper();
        var unsatInterpreter = new UnsatCoreInterpreter();
        var preflightChecker = new PreflightFeasibilityChecker();
        var engine = new CpSatSolverEngine(modelBuilder, mapper, configMock.Object, unsatInterpreter, preflightChecker);
        return (engine, configMock);
    }

    // Backward-compat for existing tests that don't care about config:
    private static CpSatSolverEngine BuildEngine() => BuildEngineWithConfig().engine;

    /// <summary>
    /// Partial-scheduling tests gibi farklı encoder kümesi gerektiren testler için yardımcı.
    /// BuildEngineWithConfig sadece ClassroomConflictEncoder kullanıyor; bu helper ile çağıran
    /// kendi encoder listesini geçer (örn. H1 + H8 same-course no-overlap).
    /// </summary>
    private static CpSatSolverEngine BuildEngineWithCustomEncoders(
        IConstraintEncoder[] encoders,
        int timeLimitSeconds = 30,
        int numWorkers = 8,
        int randomSeed = 42)
    {
        var configMock = new Mock<ISystemConfigProvider>();
        configMock.Setup(x => x.GetIntAsync("Schedule.OrTools.TimeLimitSeconds", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(timeLimitSeconds);
        configMock.Setup(x => x.GetIntAsync("Schedule.OrTools.NumWorkers", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(numWorkers);
        configMock.Setup(x => x.GetIntAsync("Schedule.OrTools.RandomSeed", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(randomSeed);
        configMock.Setup(x => x.GetBoolAsync("Schedule.OrTools.LogToConsole", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var modelBuilder = new ScheduleModelBuilder(encoders);
        var mapper = new SolutionMapper();
        var unsatInterpreter = new UnsatCoreInterpreter();
        var preflightChecker = new PreflightFeasibilityChecker();
        return new CpSatSolverEngine(modelBuilder, mapper, configMock.Object, unsatInterpreter, preflightChecker);
    }

    [Fact]
    public async Task SolveAsync_TwoNonOverlappingSections_ReturnsOptimalOrFeasible()
    {
        var engine = BuildEngine();
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[] { Section(1, 100, 3, 0), Section(2, 200, 3, 0) },
            Classrooms = new List<ClassroomInput>
            {
                new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
            },
            TimeSlots = Enumerable.Range(1, 8)
                .Select(i => new TimeSlotInput
                {
                    Id = i,
                    DayOfWeek = DayOfWeek.Monday,
                    SlotOrder = i,
                    StartTime = new TimeOnly(8 + i, 0),
                    EndTime = new TimeOnly(8 + i, 50),
                    AllowedMode = TimeSlotAllowedMode.Both
                })
                .ToList(),
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true }
        };

        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(SolverStatus.Optimal, SolverStatus.Feasible);
        result.Diagnostics.EngineVersion.Should().Be("v2-ortools");
    }

    [Fact]
    public async Task SolveAsync_PreCanceledToken_ThrowsOperationCanceledException()
    {
        var engine = BuildEngine();
        using var cts = new CancellationTokenSource();
        cts.Cancel();   // cancel BEFORE SolveAsync is called
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[] { Section(1, 100, 3, 0) },
            Classrooms = new List<ClassroomInput>
            {
                new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
            },
            TimeSlots = new List<TimeSlotInput>
            {
                new() { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                    StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                    AllowedMode = TimeSlotAllowedMode.Both }
            }
        };

        var act = async () => await engine.SolveAsync(request, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SolveAsync_ReadsSolverParametersFromConfigProvider()
    {
        var (engine, configMock) = BuildEngineWithConfig(
            timeLimitSeconds: 60,
            numWorkers: 4,
            randomSeed: 123);

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[] { Section(1, 100, 3, 0) },
            Classrooms = new List<ClassroomInput>
            {
                new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
            },
            TimeSlots = new List<TimeSlotInput>
            {
                new() { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                    StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                    AllowedMode = TimeSlotAllowedMode.Both }
            },
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true }
        };

        await engine.SolveAsync(request, CancellationToken.None);

        configMock.Verify(x => x.GetIntAsync("Schedule.OrTools.TimeLimitSeconds", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        configMock.Verify(x => x.GetIntAsync("Schedule.OrTools.NumWorkers", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        configMock.Verify(x => x.GetIntAsync("Schedule.OrTools.RandomSeed", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        configMock.Verify(x => x.GetBoolAsync("Schedule.OrTools.LogToConsole", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SolveAsync_PreflightInfeasibleSection_ExcludedFromSolver_AppendedToFailures()
    {
        // 2 sections: 1 feasible (lab section + lab room), 1 infeasible (lab section, no lab room)
        var (engine, _) = BuildEngineWithConfig();
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[]
            {
                new SectionInput
                {
                    Id = 1, CourseId = 100, CourseCode = "OK", CourseName = "Feasible",
                    SectionNumber = 1, Capacity = 40, TheoryHours = 0, LabHours = 2,
                    TeachingMethod = TeachingMethod.FaceToFace
                },
                new SectionInput
                {
                    Id = 2, CourseId = 200, CourseCode = "BAD", CourseName = "Infeasible",
                    SectionNumber = 1, Capacity = 40, TheoryHours = 0, LabHours = 2,
                    TeachingMethod = TeachingMethod.FaceToFace
                }
            },
            Classrooms = new[]
            {
                // Only 1 lab room — but section 2 needs lab; section 1 also needs lab
                // Both pass preflight. To make section 2 infeasible at preflight,
                // we'd need section 2's capacity > all lab rooms' capacity.
                new ClassroomInput { Id = 9, Name = "Lab", Capacity = 50, Type = ClassroomType.Lab }
            },
            TimeSlots = new[]
            {
                new TimeSlotInput
                {
                    Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                    StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                    AllowedMode = TimeSlotAllowedMode.Both
                }
            },
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true }
        };

        // Modify section 2's capacity to exceed lab room → preflight will mark it infeasible
        var sectionsModified = new[]
        {
            request.Sections[0],
            request.Sections[1] with { Capacity = 200 }
        };
        var modifiedRequest = request with { Sections = sectionsModified };

        var result = await engine.SolveAsync(modifiedRequest, CancellationToken.None);

        // Section 2 should be in failures (preflight rejected); section 1 may or may not be in assignments
        // depending on solver. Critical assertion: section 2 is in Failures with preflight reason.
        result.Failures.Should().Contain(f => f.SectionId == 2);
        var failure = result.Failures.First(f => f.SectionId == 2);
        failure.Reason.Should().Contain("kapasit", because: "preflight should mark section 2 as capacity-infeasible");
    }

    [Fact]
    public async Task SolveAsync_AllSectionsPreflightInfeasible_ReturnsInfeasibleStatus()
    {
        var (engine, _) = BuildEngineWithConfig();
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[]
            {
                new SectionInput
                {
                    Id = 1, CourseId = 100, CourseCode = "X", CourseName = "X",
                    SectionNumber = 1, Capacity = 40, TheoryHours = 0, LabHours = 2,
                    TeachingMethod = TeachingMethod.FaceToFace
                }
            },
            // No lab rooms at all — section 1 will be preflight-infeasible
            Classrooms = new[]
            {
                new ClassroomInput { Id = 9, Name = "Lecture", Capacity = 50, Type = ClassroomType.Lecture }
            },
            TimeSlots = new[]
            {
                new TimeSlotInput
                {
                    Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                    StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                    AllowedMode = TimeSlotAllowedMode.Both
                }
            }
        };

        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().Be(SolverStatus.Infeasible);
        result.Assignments.Should().BeEmpty();
        result.Failures.Should().HaveCount(1);
        result.Failures[0].SectionId.Should().Be(1);
    }

    [Fact]
    public async Task SolveAsync_ThreeSameCourseSectionsTwoSlots_AssignsTwoDropsOne()
    {
        // Plan 3 partial scheduling: aynı dersin 3 section'ı (H8: çakışma yok)
        // Sadece 2 zaman slot'u — max 2 section yerleşebilir, 1'i drop edilmeli.
        // NOT: H1 (classroom no-overlap) burada disabled — H1'in room optional interval'ları
        // section.present literal'ine bağlanmıyor (ayrı presence: room==classroomId), bu yüzden
        // section drop'u classroom slot'unu serbest bırakmıyor. Bu encoder bug'ı Plan 3 Task 4
        // kapsamında değil; test pure section-level partial scheduling'i izole eder.
        var engine = BuildEngineWithCustomEncoders(new IConstraintEncoder[]
        {
            new SameCourseSectionEncoder()
        });

        var sections = new[]
        {
            new SectionInput
            {
                Id = 1, CourseId = 100, CourseCode = "X", CourseName = "X",
                SectionNumber = 1, Capacity = 40, TheoryHours = 1, LabHours = 0,
                TeachingMethod = TeachingMethod.FaceToFace
            },
            new SectionInput
            {
                Id = 2, CourseId = 100, CourseCode = "X", CourseName = "X",
                SectionNumber = 2, Capacity = 40, TheoryHours = 1, LabHours = 0,
                TeachingMethod = TeachingMethod.FaceToFace
            },
            new SectionInput
            {
                Id = 3, CourseId = 100, CourseCode = "X", CourseName = "X",
                SectionNumber = 3, Capacity = 40, TheoryHours = 1, LabHours = 0,
                TeachingMethod = TeachingMethod.FaceToFace
            }
        };

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = sections,
            Classrooms = new[]
            {
                new ClassroomInput { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
            },
            TimeSlots = new[]
            {
                new TimeSlotInput
                {
                    Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                    StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                    AllowedMode = TimeSlotAllowedMode.Both
                },
                new TimeSlotInput
                {
                    Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2,
                    StartTime = new TimeOnly(9, 55), EndTime = new TimeOnly(10, 45),
                    AllowedMode = TimeSlotAllowedMode.Both
                }
            },
            ConstraintFlags = new Dictionary<string, bool>
            {
                ["H1"] = false,
                ["H8"] = true   // same-course no overlap → 1 section drop edilmek zorunda
            }
        };

        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(new[] { SolverStatus.Optimal, SolverStatus.Feasible },
            because: "partial scheduling feasible bir sonuç üretmeli, infeasible değil");
        result.Assignments.Should().HaveCount(2,
            because: "2 slot + H8 ile max 2 same-course section yerleşebilir");
        result.Failures.Should().HaveCount(1,
            because: "1 section drop edilmek zorunda (Maximize(present) objective)");
    }
}
