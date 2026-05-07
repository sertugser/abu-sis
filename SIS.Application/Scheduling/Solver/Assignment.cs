namespace SIS.Application.Scheduling.Solver;

public sealed record Assignment
{
    public required int SectionId { get; init; }
    public required IReadOnlyList<AssignedSlot> Slots { get; init; }
}

public sealed record AssignedSlot
{
    public required int TimeSlotId { get; init; }
    public required int ClassroomId { get; init; }
    public required ComponentType ComponentType { get; init; }
}
