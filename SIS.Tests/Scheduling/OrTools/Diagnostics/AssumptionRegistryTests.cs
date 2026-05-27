using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;

namespace SIS.Tests.Scheduling.OrTools.Diagnostics;

public class AssumptionRegistryTests
{
    [Fact]
    public void Register_ReturnsLiteralAndStoresMetadata()
    {
        var model = new CpModel();
        var registry = new AssumptionRegistry(model);

        var literal = registry.Register("H1", primarySectionId: 1, secondarySectionId: 2);

        literal.Should().NotBeNull();
        registry.Count.Should().Be(1);
    }

    [Fact]
    public void RegisterAllAsAssumptions_AddsAllLiteralsToModel()
    {
        var model = new CpModel();
        var registry = new AssumptionRegistry(model);
        registry.Register("H1", 1, 2);
        registry.Register("H8", 3, 4);

        registry.RegisterAllAsAssumptions();

        registry.Count.Should().Be(2);
    }

    [Fact]
    public void Lookup_ByLiteral_ReturnsMetadata()
    {
        var model = new CpModel();
        var registry = new AssumptionRegistry(model);
        var literal = registry.Register("H8", 5, 7);

        var meta = registry.LookupByLiteral(literal);

        meta.Should().NotBeNull();
        meta!.ConstraintCode.Should().Be("H8");
        meta.PrimarySectionId.Should().Be(5);
        meta.SecondarySectionId.Should().Be(7);
    }

    [Fact]
    public void Lookup_UnknownLiteral_ReturnsNull()
    {
        var model = new CpModel();
        var registry = new AssumptionRegistry(model);
        var unknownLiteral = model.NewBoolVar("foreign");

        var meta = registry.LookupByLiteral(unknownLiteral);

        meta.Should().BeNull();
    }

    [Fact]
    public void LookupByIndex_InfeasibleModel_ResolvesAssumptionFromSolverCore()
    {
        // Regresyon: solver.SufficientAssumptionsForInfeasibility() literal PROTO indekslerini döner,
        // dizi pozisyonlarını değil. Önce çok sayıda karar değişkeni yaratıp assumption literal'in
        // indeksini büyütüyoruz (gerçek modelde encoder'lar interval/room var'larından SONRA çalışır).
        // Eski pozisyon-bazlı erişim (allLiterals[idx]) burada null/yanlış dönerdi.
        var model = new CpModel();
        for (int i = 0; i < 50; i++)
            model.NewIntVar(0, 10, $"filler_{i}");

        var registry = new AssumptionRegistry(model);
        var lit = registry.Register("H1", primarySectionId: 42, secondarySectionId: null);

        // Assumption true iken model infeasible (x >= 5 ve x <= 0).
        var x = model.NewIntVar(0, 10, "x");
        model.Add(x >= 5).OnlyEnforceIf(lit);
        model.Add(x <= 0).OnlyEnforceIf(lit);

        registry.RegisterAllAsAssumptions();

        var solver = new CpSolver();
        var status = solver.Solve(model);

        status.Should().Be(CpSolverStatus.Infeasible);

        var core = solver.SufficientAssumptionsForInfeasibility();
        core.Should().NotBeEmpty();

        // Literal indeksi assumption sayısından (1) büyük → pozisyon-bazlı lookup başarısız olurdu.
        lit.GetIndex().Should().BeGreaterThan(1);

        AssumptionMetadata? resolved = null;
        foreach (var idx in core)
        {
            resolved = registry.LookupByIndex(idx);
            if (resolved != null) break;
        }

        resolved.Should().NotBeNull("UNSAT core indeksi LookupByIndex ile çözülebilmeli");
        resolved!.ConstraintCode.Should().Be("H1");
        resolved.PrimarySectionId.Should().Be(42);
    }
}
