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

public class SlotCongestionEncoderTests
{
    [Fact]
    public void Encode_FlagDisabled_NoPenalty()
    {
        var (model, ctx, _) = BuildScenario(sectionCount: 6, slotCap: 5, s3Enabled: false);
        new SlotCongestionEncoder().Encode(ctx);
        ctx.Penalties.Should().BeEmpty();
    }

    [Fact]
    public void Encode_BelowCap_NoPenalty()
    {
        // 4 sections, cap 5 → 4 ≤ 5 → 0 penalty
        var (model, ctx, _) = BuildScenario(sectionCount: 4, slotCap: 5, s3Enabled: true);
        new SlotCongestionEncoder().Encode(ctx);
        // After encode, set objective to verify
        FinishModel(model, ctx);
        var solver = new CpSolver();
        solver.Solve(model);
        // 4 present × 10000 = 40000, no penalty
        solver.ObjectiveValue.Should().Be(40000);
    }

    [Fact]
    public void Encode_AboveCap_PenaltyEqualsExcessTimesWeight()
    {
        // 7 sections, cap 5 → excess 2 → penalty = 2 × S3Weight (5) = 10
        var (model, ctx, _) = BuildScenario(sectionCount: 7, slotCap: 5, s3Enabled: true);
        new SlotCongestionEncoder().Encode(ctx);
        FinishModel(model, ctx);
        var solver = new CpSolver();
        solver.Solve(model);
        // 7 present × 10000 - 10 = 69990
        solver.ObjectiveValue.Should().Be(69990);
    }

    private static (CpModel, EncoderContext, List<SectionInput>) BuildScenario(
        int sectionCount, int slotCap, bool s3Enabled)
    {
        var model = new CpModel();
        var sections = Enumerable.Range(1, sectionCount).Select(i => new SectionInput
        {
            Id = i,
            CourseId = i,
            CourseCode = $"C{i}",
            CourseName = $"C{i}",
            SectionNumber = 1,
            Capacity = 30,
            TheoryHours = 1,
            LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace
        }).ToList();
        var classrooms = Enumerable.Range(1, sectionCount).Select(i => new ClassroomInput
        {
            Id = i,
            Name = $"R{i}",
            Capacity = 50,
            Type = ClassroomType.Lecture
        }).ToArray();
        var slots = new[]
        {
            new TimeSlotInput
            {
                Id = 1,
                DayOfWeek = DayOfWeek.Monday,
                SlotOrder = 1,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both
            }
        };

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = sections,
            Classrooms = classrooms,
            TimeSlots = slots,
            ConstraintFlags = new Dictionary<string, bool>(),
            SoftThresholds = new SoftThresholds
            {
                S3MaxSectionsPerSlot = slotCap,
                S3Weight = 5
            }
        };

        var variables = new DecisionVariables(model, totalSlots: 1);
        foreach (var s in sections)
        {
            variables.CreateIntervalsForSection(s, slots);
            variables.CreateRoomVariable(s.Id, ComponentType.Lecture, classrooms);
            // Force presence
            model.Add(((BoolVar)variables.GetSectionPresent(s.Id)) == 1);
        }

        var ctx = new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = request,
            ConstraintFlags = new Dictionary<string, bool> { ["S3"] = s3Enabled },
            Penalties = new List<LinearExpr>()
        };
        return (model, ctx, sections);
    }

    private static void FinishModel(CpModel model, EncoderContext ctx)
    {
        var presence = ctx.Variables.AllSectionPresent.Values.OfType<BoolVar>().ToList();
        LinearExpr obj = LinearExpr.Sum(presence) * 10000;
        if (ctx.Penalties.Count > 0) obj -= LinearExpr.Sum(ctx.Penalties);
        model.Maximize(obj);
    }
}
