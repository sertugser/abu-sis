using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;
using Xunit;

namespace SIS.Tests.Scheduling.OrTools.Model;

public class ScheduleModelBuilderObjectiveTests
{
    [Fact]
    public void Build_NoPenalties_ObjectiveIsW10000TimesPresence()
    {
        var request = MinimalOneSectionRequest();
        var builder = new ScheduleModelBuilder(Array.Empty<IConstraintEncoder>());

        var result = builder.Build(request);
        var solver = new CpSolver();
        var status = solver.Solve(result.Model);

        status.Should().BeOneOf(new[] { CpSolverStatus.Optimal, CpSolverStatus.Feasible });
        // 1 section present → objective ≥ 10000 (no penalties)
        solver.ObjectiveValue.Should().BeGreaterThanOrEqualTo(10000);
    }

    [Fact]
    public void Build_WithPenaltyEncoder_ObjectiveDecreases()
    {
        var request = MinimalOneSectionRequest();
        var encoder = new TestPenaltyEncoder(penaltyValue: 100);
        var builder = new ScheduleModelBuilder(new IConstraintEncoder[] { encoder });

        var result = builder.Build(request);
        var solver = new CpSolver();
        var status = solver.Solve(result.Model);

        status.Should().BeOneOf(new[] { CpSolverStatus.Optimal, CpSolverStatus.Feasible });
        // 1 present × 10000 - 100 = 9900
        solver.ObjectiveValue.Should().Be(9900);
    }

    private static SolverRequest MinimalOneSectionRequest()
    {
        var section = new SectionInput
        {
            Id = 1, CourseId = 1, CourseCode = "X", CourseName = "X",
            SectionNumber = 1, Capacity = 30,
            TheoryHours = 1, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace
        };
        var slot = new TimeSlotInput
        {
            Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
            StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
            AllowedMode = TimeSlotAllowedMode.Both
        };
        var room = new ClassroomInput { Id = 1, Name = "L1", Capacity = 50, Type = ClassroomType.Lecture };
        return new SolverRequest
        {
            TermId = 1, FacultyId = 1,
            Sections = new[] { section },
            Classrooms = new[] { room },
            TimeSlots = new[] { slot },
            ConstraintFlags = new Dictionary<string, bool>()
        };
    }

    private sealed class TestPenaltyEncoder : IConstraintEncoder
    {
        private readonly int _penalty;
        public TestPenaltyEncoder(int penaltyValue) => _penalty = penaltyValue;
        public string Code => "TEST";
        public bool IsHard => false;
        public void Encode(EncoderContext ctx)
        {
            ctx.Penalties.Add(LinearExpr.Constant(_penalty));
        }
    }
}
