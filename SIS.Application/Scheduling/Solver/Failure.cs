using System.Collections.Generic;

namespace SIS.Application.Scheduling.Solver;

public sealed record Failure
{
    public required int SectionId { get; init; }
    public required string Reason { get; init; }
    public IReadOnlyList<UnsatCoreEntry> RelatedConstraints { get; init; } = [];
}
