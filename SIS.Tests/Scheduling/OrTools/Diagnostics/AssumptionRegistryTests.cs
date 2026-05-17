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
}
