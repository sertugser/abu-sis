using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// Plan 3 Tasks 5-11 — Soft constraint diagnostic stubs.
/// All 7 soft encoders share an identical contract: correct Code, IsHard=false,
/// and zero penalty/assumption side-effects whether the flag is enabled or disabled.
/// Real penalty math is deferred to Plan 4.
/// </summary>
public class SoftEncodersTests
{
    private static EncoderContext BuildContext(IReadOnlyDictionary<string, bool>? flags = null)
    {
        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: 50);
        return new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = new SolverRequest
            {
                TermId = 1,
                FacultyId = 1,
                Sections = new List<SectionInput>(),
                Classrooms = new List<ClassroomInput>(),
                TimeSlots = new List<TimeSlotInput>()
            },
            ConstraintFlags = flags ?? new Dictionary<string, bool>(),
            Penalties = new List<LinearExpr>()
        };
    }

    public static IEnumerable<object[]> AllSoftEncoders()
    {
        yield return new object[] { new OccupancyEncoder(), "S1" };
        yield return new object[] { new SlotCongestionEncoder(), "S3" };
        yield return new object[] { new CoEnrollmentEncoder(), "S4" };
        yield return new object[] { new StudentGapEncoder(), "S7" };
        yield return new object[] { new WeekdayBalanceEncoder(), "S12" };
        yield return new object[] { new AdjacentYearEncoder(), "S20" };
    }

    [Theory]
    [MemberData(nameof(AllSoftEncoders))]
    public void Encoder_HasCorrectCodeAndIsSoft(IConstraintEncoder encoder, string expectedCode)
    {
        encoder.Code.Should().Be(expectedCode);
        encoder.IsHard.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(AllSoftEncoders))]
    public void Encoder_WhenDisabled_DoesNotAddPenalty(IConstraintEncoder encoder, string code)
    {
        var ctx = BuildContext(flags: new Dictionary<string, bool> { [code] = false });

        encoder.Encode(ctx);

        ctx.Penalties.Should().BeEmpty();
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(AllSoftEncoders))]
    public void Encoder_WhenEnabledWithEmptyRequest_StillProducesNoPenalty(IConstraintEncoder encoder, string code)
    {
        // Plan 3 diagnostic stub: enabled but doesn't add penalty math
        var ctx = BuildContext(flags: new Dictionary<string, bool> { [code] = true });

        encoder.Encode(ctx);

        ctx.Penalties.Should().BeEmpty();
        ctx.Assumptions.Count.Should().Be(0);
    }
}
