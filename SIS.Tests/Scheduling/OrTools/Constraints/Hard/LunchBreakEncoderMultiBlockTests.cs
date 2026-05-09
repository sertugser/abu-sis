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
/// Plan 9 Issue 3 — H13 LunchBreak multi-block lab parity.
///
/// Mirrors Plan 8 Task 6 (S5 LunchBreakSoftEncoder polish) but for the hard tier.
/// Pre-fix: LunchBreakEncoder lab branch used GetLabInterval (singular, Plan 4 era)
/// which only constrained the FIRST block — non-primary blocks could land in lunch slots.
/// Post-fix: iterate GetLabIntervals (plural, Plan 7b Task 7) — every block forbidden.
/// </summary>
public class LunchBreakEncoderMultiBlockTests
{
    /// <summary>
    /// 5-day grid × 6 slots/day with NO gaps (so 12:00 is a valid 2-h block start).
    /// Each day: 09:00, 10:00, 11:00, 12:00*, 13:00, 14:00 — back-to-back hourly slots.
    /// (* lunch slot: 12:00 ≥ 11:40 and 12:00 &lt; 12:30 → flagged by H13 lunch window)
    /// SlotBlockValidator allows 2-h blocks starting at any of indices {0..4} since
    /// gaps are zero. The lunch slot at index 3 is a valid 2-h block start.
    /// </summary>
    private static IReadOnlyList<TimeSlotInput> BuildWeekGridWithLunch()
    {
        var slots = new List<TimeSlotInput>();
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        var slotTimes = new (TimeOnly start, TimeOnly end)[]
        {
            (new TimeOnly(9, 0),  new TimeOnly(9, 50)),
            (new TimeOnly(10, 0), new TimeOnly(10, 50)),
            (new TimeOnly(11, 0), new TimeOnly(11, 50)),
            (new TimeOnly(12, 0), new TimeOnly(12, 50)),  // lunch slot (12:00 ∈ [11:40, 12:30))
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
    public void Encode_MultiBlockLab_NonPrimaryBlockForcedIntoLunchSlot_IsInfeasible()
    {
        // Arrange: section LabHours=4, MaxLabBlockHours=2 → 2 lab blocks of 2h each.
        // Lunch window 11:40-12:30; slot index 3 (Mon 12:00-12:50) is a lunch slot.
        // Strategy: force the SECOND lab block (intervals[1]) to start at a lunch slot.
        //   - Pre-fix (singular GetLabInterval): only block 0 constrained → block 1 free → SAT
        //   - Post-fix (plural GetLabIntervals): all blocks constrained → forcing block 1
        //     to lunch slot conflicts with H13 → UNSAT.
        // The test asserts UNSAT, which fails without the plural fix.
        var slots = BuildWeekGridWithLunch();
        var section = new SectionInput
        {
            Id = 1,
            CourseId = 100,
            CourseCode = "LAB101",
            CourseName = "Lab Course",
            SectionNumber = 1,
            Capacity = 30,
            TheoryHours = 0,
            LabHours = 4,
            TeachingMethod = TeachingMethod.FaceToFace
        };

        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: slots.Count);
        // Plan 7b Task 7: skip single-interval lab; multi-interval method creates blocks.
        variables.CreateIntervalsForSection(section, slots, createLabInterval: false);
        variables.CreateLabIntervalsForSection(section, slots, maxLabBlockHours: 2);

        // Force section to be present so the optional intervals are active.
        var present = (BoolVar)variables.GetSectionPresent(section.Id);
        model.Add(present == 1);

        var ctx = new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = new SolverRequest
            {
                TermId = 1,
                FacultyId = 1,
                Sections = new[] { section },
                Classrooms = new List<ClassroomInput>(),
                TimeSlots = slots
            },
            ConstraintFlags = new Dictionary<string, bool> { ["H13"] = true },
            Penalties = new List<LinearExpr>()
        };

        // Lunch window: 11:40-12:30 → catches slot index 3 (Mon 12:00) and similar per day.
        var encoder = new LunchBreakEncoder(
            lunchStart: new TimeOnly(11, 40), lunchEnd: new TimeOnly(12, 30));

        // Act — encode H13 constraint.
        encoder.Encode(ctx);

        // Compute the set of lunch slot indices (slot.StartTime ∈ [11:40, 12:30)).
        var lunchSlotIndices = new List<int>();
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s.StartTime >= new TimeOnly(11, 40) && s.StartTime < new TimeOnly(12, 30))
                lunchSlotIndices.Add(i);
        }
        lunchSlotIndices.Should().NotBeEmpty(
            because: "grid contains 12:00-12:50 slot per day");

        var labIntervals = variables.GetLabIntervals(section.Id);
        labIntervals.Should().HaveCount(2,
            because: "LabHours=4 / MaxLabBlockHours=2 → 2 blocks");

        // Force the SECOND block start to a lunch slot. With the plural fix, H13 makes
        // this UNSAT (block 1 also constrained); without the fix, block 1 is free → SAT.
        var lunchSlotForBlock1 = lunchSlotIndices[0];  // e.g., Monday 12:00 (idx 3)
        model.Add(labIntervals[1].StartExpr() == lunchSlotForBlock1);

        // Promote H13 assumption literals to assumptions so OnlyEnforceIf fires.
        ctx.Assumptions.RegisterAllAsAssumptions();

        // Solve — expect INFEASIBLE because block 1 is forced into a lunch slot
        // and H13 (post-fix) forbids any block from starting in lunch slots.
        var solver = new CpSolver();
        var status = solver.Solve(model);

        status.Should().Be(CpSolverStatus.Infeasible,
            because: "post-fix H13 must constrain ALL lab blocks (plural GetLabIntervals); " +
                     "forcing block 1 into a lunch slot must produce UNSAT. " +
                     "Pre-fix this would be SAT (only block 0 constrained via singular GetLabInterval).");
    }
}
