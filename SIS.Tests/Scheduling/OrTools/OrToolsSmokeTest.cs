using FluentAssertions;
using Google.OrTools.Sat;

namespace SIS.Tests.Scheduling.OrTools;

public class OrToolsSmokeTest
{
    [Fact]
    public void CpSat_TrivialFeasibleModel_ReturnsOptimal()
    {
        // Arrange
        var model = new CpModel();
        var x = model.NewIntVar(0, 10, "x");
        var y = model.NewIntVar(0, 10, "y");
        model.Add(x + y == 12);
        model.Add(x >= 5);

        // Act
        var solver = new CpSolver();
        var status = solver.Solve(model);

        // Assert
        status.Should().Be(CpSolverStatus.Optimal);
        solver.Value(x).Should().BeGreaterThanOrEqualTo(5);
        (solver.Value(x) + solver.Value(y)).Should().Be(12);
    }

    [Fact]
    public void CpSat_InfeasibleModelWithAssumptions_ReturnsUnsatCore()
    {
        // Arrange — bilerek çelişkili kısıtlar
        var model = new CpModel();
        var x = model.NewIntVar(0, 10, "x");
        var y = model.NewIntVar(0, 10, "y");
        var a = model.NewBoolVar("a");
        var b = model.NewBoolVar("b");

        model.Add(x > y).OnlyEnforceIf(a);
        model.Add(y > x).OnlyEnforceIf(b);
        model.AddAssumptions(new ILiteral[] { a, b });

        // Act
        var solver = new CpSolver();
        var status = solver.Solve(model);
        var coreIndices = solver.SufficientAssumptionsForInfeasibility();

        // Assert
        status.Should().Be(CpSolverStatus.Infeasible);
        coreIndices.Should().NotBeEmpty();
        coreIndices.Should().HaveCountLessThanOrEqualTo(2);
    }
}
