using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;
using Xunit;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// Plan 9 Issue 4a — S1 OccupancyEncoder multi-block lab presence literal coverage.
///
/// Pre-fix: encoder iterated only AllPresenceLiterals (single-room) and
/// AllPresenceLiteralsForGroups (Plan 4 parallel labs) — multi-block labs registered
/// via AllPresenceLiteralsForLabBlocks (Plan 7b time-split) accrued zero S1 penalty.
/// Post-fix: third foreach over AllPresenceLiteralsForLabBlocks adds one penalty term
/// per (section × classroom × block × group) presence literal.
/// </summary>
public class OccupancyEncoderMultiBlockTests
{
    /// <summary>
    /// 5-day grid × 5 slots/day, back-to-back hourly slots. Allows 2-h block starts at
    /// indices {0..3} per day (no cross-day gaps because slots are contiguous within a day).
    /// </summary>
    private static IReadOnlyList<TimeSlotInput> BuildWeekGrid()
    {
        var slots = new List<TimeSlotInput>();
        var days = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday
        };
        var slotTimes = new (TimeOnly start, TimeOnly end)[]
        {
            (new TimeOnly(9, 0),  new TimeOnly(9, 50)),
            (new TimeOnly(10, 0), new TimeOnly(10, 50)),
            (new TimeOnly(11, 0), new TimeOnly(11, 50)),
            (new TimeOnly(13, 0), new TimeOnly(13, 50)),
            (new TimeOnly(14, 0), new TimeOnly(14, 50)),
        };
        int id = 1;
        foreach (var day in days)
        {
            for (int s = 0; s < slotTimes.Length; s++)
            {
                slots.Add(new TimeSlotInput
                {
                    Id = id++,
                    DayOfWeek = day,
                    SlotOrder = s + 1,
                    StartTime = slotTimes[s].start,
                    EndTime = slotTimes[s].end,
                    AllowedMode = TimeSlotAllowedMode.Both
                });
            }
        }
        return slots;
    }

    [Fact]
    public void Encode_MultiBlockLab_AddsPenaltyPerBlockPresenceLiteral()
    {
        // Arrange: section LabHours=4, MaxLabBlockHours=2 → 2 lab blocks of 2h each.
        // LabGroupCount=1, 1 classroom → AllPresenceLiteralsForLabBlocks has 2 entries
        // (one per (section, classroom, block, group=0) tuple).
        // Section.Capacity=10, Classroom.Capacity=100 → rate = 10 < min (60) →
        // deviation = 50, weight = 1, so each presence literal contributes a penalty term.
        // Expected: ctx.Penalties.Count == 2 (one per block presence literal).
        var slots = BuildWeekGrid();
        var section = new SectionInput
        {
            Id = 1,
            CourseId = 100,
            CourseCode = "LAB101",
            CourseName = "Lab Course",
            SectionNumber = 1,
            Capacity = 10,
            TheoryHours = 0,
            LabHours = 4,
            LabGroupCount = 1,
            TeachingMethod = TeachingMethod.FaceToFace,
        };
        var classroom = new ClassroomInput
        {
            Id = 10,
            Name = "L-101",
            Capacity = 100,
            Type = ClassroomType.Lab,
        };

        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: slots.Count);

        // Plan 7b Task 7: skip single-interval lab; multi-interval method creates blocks.
        variables.CreateIntervalsForSection(section, slots, createLabInterval: false);
        variables.CreateLabIntervalsForSection(section, slots, maxLabBlockHours: 2);

        // Lab room IntVar (groupCount=1).
        variables.CreateRoomVariable(section.Id, ComponentType.Lab, new[] { classroom }, groupCount: 1);

        // Register the per-(section, classroom, block, group) optional intervals so the
        // AllPresenceLiteralsForLabBlocks dict gets populated. Caller invariant requires
        // sourceInterval = GetLabIntervals(sectionId)[blockIndex].
        var labIntervals = variables.GetLabIntervals(section.Id);
        labIntervals.Should().HaveCount(2,
            because: "LabHours=4 / MaxLabBlockHours=2 → 2 blocks");

        for (int b = 0; b < labIntervals.Count; b++)
        {
            variables.CreateOptionalRoomIntervalForLabBlock(
                section.Id, classroom.Id,
                blockIndex: b, groupIndex: 0,
                sourceInterval: labIntervals[b], duration: 2);
        }

        variables.AllPresenceLiteralsForLabBlocks.Should().HaveCount(2,
            because: "2 blocks × 1 group × 1 classroom");

        // Force every block presence literal to TRUE so penalty terms are non-zero
        // (otherwise solver could pick presence=0 to dodge penalty).
        foreach (var literal in variables.AllPresenceLiteralsForLabBlocks.Values)
            model.Add(((BoolVar)literal) == 1);

        var ctx = new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = new SolverRequest
            {
                TermId = 1,
                FacultyId = 1,
                Sections = new List<SectionInput> { section },
                Classrooms = new List<ClassroomInput> { classroom },
                TimeSlots = slots,
            },
            ConstraintFlags = new Dictionary<string, bool> { ["S1"] = true },
            Penalties = new List<LinearExpr>(),
        };

        // Act
        new OccupancyEncoder().Encode(ctx);

        // Assert: 2 penalty terms (one per block presence literal).
        // Pre-fix this would be 0 (encoder never iterated AllPresenceLiteralsForLabBlocks).
        ctx.Penalties.Should().HaveCount(2,
            because: "post-fix S1 must add a penalty term per (section, classroom, block, group) " +
                     "presence literal in AllPresenceLiteralsForLabBlocks. " +
                     "Pre-fix this was 0 — multi-block labs accrued zero S1 penalty.");
    }
}
