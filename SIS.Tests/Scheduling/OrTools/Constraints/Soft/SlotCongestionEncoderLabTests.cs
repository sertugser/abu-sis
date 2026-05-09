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
/// Plan 9 Issue 4b — S3 SlotCongestionEncoder lab interval iteration.
///
/// Pre-fix: encoder iterated only lecture intervals; lab-only sections (TheoryHours=0)
/// had GetLectureInterval==null and were skipped. With every section lab-only,
/// slotLiterals stayed empty for every slot → zero penalty terms emitted.
/// v1 SlotCongestionEvaluator counts ALL TimeBlocks (theory + lab) → divergence.
///
/// Post-fix: per-slot loop also iterates GetLabIntervals (plural — includes
/// multi-block time-split). At least one reified literal per slot ⇒ penalty term added.
/// </summary>
public class SlotCongestionEncoderLabTests
{
    /// <summary>
    /// 1 day × 4 back-to-back slots. For LabHours=2 / MaxLabBlockHours=2 single-block lab,
    /// valid block starts = {0, 1, 2}. Enough domain to make the test deterministic.
    /// </summary>
    private static IReadOnlyList<TimeSlotInput> BuildSimpleGrid()
    {
        return new[]
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
                StartTime = new TimeOnly(9, 50), EndTime = new TimeOnly(10, 40),
                AllowedMode = TimeSlotAllowedMode.Both,
            },
            new TimeSlotInput
            {
                Id = 3, DayOfWeek = DayOfWeek.Monday, SlotOrder = 3,
                StartTime = new TimeOnly(10, 40), EndTime = new TimeOnly(11, 30),
                AllowedMode = TimeSlotAllowedMode.Both,
            },
            new TimeSlotInput
            {
                Id = 4, DayOfWeek = DayOfWeek.Monday, SlotOrder = 4,
                StartTime = new TimeOnly(11, 30), EndTime = new TimeOnly(12, 20),
                AllowedMode = TimeSlotAllowedMode.Both,
            },
        };
    }

    [Fact]
    public void Encode_LabOnlySection_ContributesToSlotCongestionCount()
    {
        // Arrange: single lab-only section (TheoryHours=0, LabHours=2). MaxLabBlockHours=2
        // → single-block. Pre-fix encoder skipped lecture-null sections AND never iterated
        // lab intervals → zero penalty terms. Post-fix: one reified literal per slot from
        // GetLabIntervals → penalty term added per slot.
        var slots = BuildSimpleGrid();
        var section = new SectionInput
        {
            Id = 1,
            CourseId = 100,
            CourseCode = "LAB101",
            CourseName = "Lab Course",
            SectionNumber = 1,
            Capacity = 20,
            TheoryHours = 0,
            LabHours = 2,
            LabGroupCount = 1,
            TeachingMethod = TeachingMethod.FaceToFace,
        };
        var classroom = new ClassroomInput
        {
            Id = 10,
            Name = "L-101",
            Capacity = 30,
            Type = ClassroomType.Lab,
        };

        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: slots.Count);

        // Plan 7b Task 7: skip single-interval lab so multi-interval method owns the lab var.
        variables.CreateIntervalsForSection(section, slots, createLabInterval: false);
        variables.CreateLabIntervalsForSection(section, slots, maxLabBlockHours: 2);
        variables.CreateRoomVariable(section.Id, ComponentType.Lab, new[] { classroom }, groupCount: 1);

        // Force presence so the lab interval is "real" (start IntVar must take a value
        // in the valid-starts domain).
        model.Add(((BoolVar)variables.GetSectionPresent(section.Id)) == 1);

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new List<SectionInput> { section },
            Classrooms = new List<ClassroomInput> { classroom },
            TimeSlots = slots,
            MaxLabBlockHours = 2,
            SoftThresholds = new SoftThresholds
            {
                S3MaxSectionsPerSlot = 5,
                S3Weight = 5,
            },
        };

        var ctx = new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = request,
            ConstraintFlags = new Dictionary<string, bool> { ["S3"] = true },
            Penalties = new List<LinearExpr>(),
        };

        // Act
        new SlotCongestionEncoder().Encode(ctx);

        // Assert: at least one slot must have produced a penalty term. Pre-fix this was 0
        // (no lecture interval ⇒ slotLiterals empty ⇒ continue). Post-fix the lab interval
        // contributes a reified literal per slot, triggering the penalty branch.
        ctx.Penalties.Should().NotBeEmpty(
            because: "post-fix S3 must iterate GetLabIntervals so lab-only sections " +
                     "contribute to per-slot count (v1 SlotCongestionEvaluator parity). " +
                     "Pre-fix this was 0 — lab-only sections accrued zero S3 penalty.");
    }
}
