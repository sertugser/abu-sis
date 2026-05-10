using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;
using Xunit;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// Plan 8 Task 17 — DIST SameRoom (hard + soft tier).
/// Hard mode: roomA == roomB enforced via model.Add.
/// Soft mode: reified diffLit = (roomA != roomB), penalty = diffLit × softWeight.
/// Online sections have no lecture room IntVar — encoder skips silently.
/// </summary>
public class DistributionConstraintEncoderSameRoomTests
{
    [Fact]
    public void Encode_SameRoom_HardMode_ForcesSameRoom()
    {
        // Hard mode: Unavailable. 2 sections + 2 classrooms (free choice).
        // Encoder must add roomA == roomB → solver places both in same room.
        var (model, ctx, sections, classrooms) = BuildScenario(
            prefLevel: PreferenceLevel.Unavailable,
            sectionAOnline: false,
            forceARoomId: null,
            forceBRoomId: null);

        new DistributionConstraintEncoder().Encode(ctx);
        FinishModel(model, ctx);

        var solver = new CpSolver();
        var status = solver.Solve(model);

        status.Should().BeOneOf(CpSolverStatus.Optimal, CpSolverStatus.Feasible);

        var roomA = ctx.Variables.GetLectureRoom(sections[0].Id);
        var roomB = ctx.Variables.GetLectureRoom(sections[1].Id);
        solver.Value(roomA).Should().Be(solver.Value(roomB),
            because: "hard SameRoom forces both lectures into the same classroom");
    }

    [Fact]
    public void Encode_SameRoom_SoftDiscouraged_DifferentRoomPenalty()
    {
        // Soft mode: Discouraged. Force section A to room 1, section B to room 2 → different rooms.
        // Penalty fires: 1 × DistDiscouragedWeight (50).
        // Objective = 2 sections × 10000 - 50 = 19950.
        var (model, ctx, _, _) = BuildScenario(
            prefLevel: PreferenceLevel.Discouraged,
            sectionAOnline: false,
            forceARoomId: 1,
            forceBRoomId: 2);

        new DistributionConstraintEncoder().Encode(ctx);
        FinishModel(model, ctx);

        var solver = new CpSolver();
        var status = solver.Solve(model);

        status.Should().BeOneOf(CpSolverStatus.Optimal, CpSolverStatus.Feasible);
        solver.ObjectiveValue.Should().Be(20000 - 50);
    }

    [Fact]
    public void Encode_SameRoom_SoftStronglyDiscouraged_HigherPenalty()
    {
        // Soft mode: StronglyDiscouraged. Same different-room setup (room 1 + room 2).
        // Penalty: 1 × DistStronglyDiscouragedWeight (200).
        // Objective = 20000 - 200 = 19800.
        var (model, ctx, _, _) = BuildScenario(
            prefLevel: PreferenceLevel.StronglyDiscouraged,
            sectionAOnline: false,
            forceARoomId: 1,
            forceBRoomId: 2);

        new DistributionConstraintEncoder().Encode(ctx);
        FinishModel(model, ctx);

        var solver = new CpSolver();
        var status = solver.Solve(model);

        status.Should().BeOneOf(CpSolverStatus.Optimal, CpSolverStatus.Feasible);
        solver.ObjectiveValue.Should().Be(20000 - 200);
    }

    [Fact]
    public void Encode_SameRoom_OnlineSection_Skipped()
    {
        // Section A is Online → no lecture room IntVar created. Section B FaceToFace.
        // Encoder must guard and return silently (no exception, no penalty).
        // Even with Discouraged soft mode + forced different-where-possible, no penalty term emerges.
        var (model, ctx, _, _) = BuildScenario(
            prefLevel: PreferenceLevel.Discouraged,
            sectionAOnline: true,
            forceARoomId: null,
            forceBRoomId: 2);

        var act = () => new DistributionConstraintEncoder().Encode(ctx);
        act.Should().NotThrow(
            because: "Online section has no lecture room var; SameRoom encoder must skip silently");

        FinishModel(model, ctx);

        var solver = new CpSolver();
        var status = solver.Solve(model);

        status.Should().BeOneOf(CpSolverStatus.Optimal, CpSolverStatus.Feasible);
        // No penalty applied; objective = 2 × 10000 = 20000.
        solver.ObjectiveValue.Should().Be(20000);
    }

    /// <summary>
    /// 2 sections (CourseId 1, 2), TheoryHours=1 each. Section A's TeachingMethod toggleable
    /// (FaceToFace by default; Online when <paramref name="sectionAOnline"/> is true).
    /// 2 classrooms (id 1, 2). 4-slot Monday grid. 1 dist constraint over both courses.
    /// Force presence == 1. Force lecture room ids when requested.
    /// </summary>
    private static (CpModel model, EncoderContext ctx, SectionInput[] sections,
        ClassroomInput[] classrooms) BuildScenario(
        PreferenceLevel prefLevel,
        bool sectionAOnline,
        int? forceARoomId,
        int? forceBRoomId)
    {
        var sections = new[]
        {
            new SectionInput
            {
                Id = 1,
                CourseId = 1,
                CourseCode = "C1",
                CourseName = "C1",
                SectionNumber = 1,
                Capacity = 30,
                TheoryHours = 1,
                LabHours = 0,
                TeachingMethod = sectionAOnline ? TeachingMethod.Online : TeachingMethod.FaceToFace,
            },
            new SectionInput
            {
                Id = 2,
                CourseId = 2,
                CourseCode = "C2",
                CourseName = "C2",
                SectionNumber = 1,
                Capacity = 30,
                TheoryHours = 1,
                LabHours = 0,
                TeachingMethod = TeachingMethod.FaceToFace,
            },
        };

        // 1-day × 4-slot grid (room is the cisim, day uniformity does not matter).
        var slots = new[]
        {
            new TimeSlotInput
            {
                Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both,
            },
            new TimeSlotInput
            {
                Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2,
                StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 50),
                AllowedMode = TimeSlotAllowedMode.Both,
            },
            new TimeSlotInput
            {
                Id = 3, DayOfWeek = DayOfWeek.Monday, SlotOrder = 3,
                StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(11, 50),
                AllowedMode = TimeSlotAllowedMode.Both,
            },
            new TimeSlotInput
            {
                Id = 4, DayOfWeek = DayOfWeek.Monday, SlotOrder = 4,
                StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(12, 50),
                AllowedMode = TimeSlotAllowedMode.Both,
            },
        };

        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "R1", Capacity = 50, Type = ClassroomType.Lecture },
            new ClassroomInput { Id = 2, Name = "R2", Capacity = 50, Type = ClassroomType.Lecture },
        };

        var dist = new DistributionConstraintInput(
            Id: 42,
            DistributionType: DistributionType.SameRoom,
            PreferenceLevel: prefLevel,
            Courses: new[]
            {
                new DistributionCourseInput(1, null, 0),
                new DistributionCourseInput(2, null, 1),
            });

        var flags = new Dictionary<string, bool> { ["DIST"] = true };

        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: slots.Length);

        foreach (var s in sections)
        {
            variables.CreateIntervalsForSection(s, slots);

            // Online sections have no lecture room var (Plan 7b Task 4 — ScheduleModelBuilder
            // skips CreateRoomVariable for Online). Mirror that here so the encoder must guard.
            if (s.TeachingMethod != TeachingMethod.Online)
            {
                variables.CreateRoomVariable(s.Id, ComponentType.Lecture, classrooms);
            }

            // Force section presence == 1 so penalty terms cannot be optimised away.
            model.Add(((BoolVar)variables.GetSectionPresent(s.Id)) == 1);
        }

        // Pin lecture rooms when requested (only if room var exists).
        if (forceARoomId.HasValue && sections[0].TeachingMethod != TeachingMethod.Online)
        {
            model.Add(variables.GetLectureRoom(sections[0].Id) == forceARoomId.Value);
        }
        if (forceBRoomId.HasValue && sections[1].TeachingMethod != TeachingMethod.Online)
        {
            model.Add(variables.GetLectureRoom(sections[1].Id) == forceBRoomId.Value);
        }

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = sections,
            Classrooms = classrooms,
            TimeSlots = slots,
            ConstraintFlags = flags,
            DistributionConstraints = new[] { dist },
            SoftThresholds = new SoftThresholds
            {
                DistDiscouragedWeight = 50,
                DistStronglyDiscouragedWeight = 200,
            },
        };

        var ctx = new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = request,
            ConstraintFlags = flags,
            Penalties = new List<LinearExpr>(),
        };

        return (model, ctx, sections, classrooms);
    }

    /// <summary>
    /// Standard objective: maximise (sum(presence) × 10000) - sum(penalties).
    /// 2 sections × 10000 = 20000 baseline.
    /// </summary>
    private static void FinishModel(CpModel model, EncoderContext ctx)
    {
        var presence = ctx.Variables.AllSectionPresent.Values.OfType<BoolVar>().ToList();
        LinearExpr obj = LinearExpr.Sum(presence) * 10000;
        if (ctx.Penalties.Count > 0) obj -= LinearExpr.Sum(ctx.Penalties);
        model.Maximize(obj);
    }
}
