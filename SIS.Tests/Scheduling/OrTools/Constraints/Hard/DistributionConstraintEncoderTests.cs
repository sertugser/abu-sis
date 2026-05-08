using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class DistributionConstraintEncoderTests
{
    [Fact]
    public void Code_IsDist_AndIsHard()
    {
        var encoder = new DistributionConstraintEncoder();

        encoder.Code.Should().Be("DIST");
        encoder.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Encode_OneDistConstraint_RegistersAssumption()
    {
        var dist = new DistributionConstraintInput(
            Id: 1,
            DistributionType: DistributionType.SameDay,
            PreferenceLevel: PreferenceLevel.Unavailable,
            Courses: new[]
            {
                new DistributionCourseInput(100, null, 0),
                new DistributionCourseInput(200, null, 1)
            });
        var encoder = new DistributionConstraintEncoder();
        var ctx = BuildContext(new[] { dist });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_NoConstraints_NoAssumption()
    {
        var encoder = new DistributionConstraintEncoder();
        var ctx = BuildContext(Array.Empty<DistributionConstraintInput>());

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_FlagDisabled_NoOp()
    {
        var dist = new DistributionConstraintInput(
            1, DistributionType.SameDay, PreferenceLevel.Unavailable,
            new[] { new DistributionCourseInput(100, null, 0) });
        var encoder = new DistributionConstraintEncoder();
        var ctx = BuildContext(new[] { dist },
            flags: new Dictionary<string, bool> { ["DIST"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_ThreeConstraints_RegistersThreeAssumptions()
    {
        var dists = new[]
        {
            new DistributionConstraintInput(1, DistributionType.SameDay, PreferenceLevel.Unavailable,
                new[] { new DistributionCourseInput(100, null, 0) }),
            new DistributionConstraintInput(2, DistributionType.DifferentDay, PreferenceLevel.Discouraged,
                new[] { new DistributionCourseInput(200, null, 0) }),
            new DistributionConstraintInput(3, DistributionType.BackToBack, PreferenceLevel.Unavailable,
                new[] { new DistributionCourseInput(300, null, 0) })
        };
        var encoder = new DistributionConstraintEncoder();
        var ctx = BuildContext(dists);

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(3);
    }

    private static EncoderContext BuildContext(
        IReadOnlyList<DistributionConstraintInput> distConstraints,
        IReadOnlyDictionary<string, bool>? flags = null)
    {
        flags ??= new Dictionary<string, bool> { ["DIST"] = true };

        var sections = Array.Empty<SectionInput>();
        var classrooms = new[]
        {
            new ClassroomInput { Id = 9, Name = "C", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var slots = Enumerable.Range(0, 4).Select(i => new TimeSlotInput
        {
            Id = i + 1,
            DayOfWeek = DayOfWeek.Monday,
            SlotOrder = i + 1,
            StartTime = new TimeOnly(9 + i, 0),
            EndTime = new TimeOnly(9 + i, 50),
            AllowedMode = TimeSlotAllowedMode.Both
        }).ToList();

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = sections,
            Classrooms = classrooms,
            TimeSlots = slots,
            ConstraintFlags = flags,
            DistributionConstraints = distConstraints
        };

        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: slots.Count);
        var assumptions = new AssumptionRegistry(model);

        return new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = assumptions,
            Request = request,
            ConstraintFlags = flags,
            Penalties = new List<LinearExpr>()
        };
    }
}
