using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.Scheduling.Constraints;

public sealed class ConstraintResult
{
    public List<SchedulingConflictDto> Conflicts { get; init; } = [];
    public List<string> Notes { get; init; } = [];
    public double SoftPenalty { get; init; }

    public static ConstraintResult Ok() => new();

    public static ConstraintResult WithConflict(SchedulingConflictDto conflict) =>
        new() { Conflicts = [conflict] };

    public static ConstraintResult WithConflicts(List<SchedulingConflictDto> conflicts) =>
        new() { Conflicts = conflicts };

    public static ConstraintResult WithNote(string note) =>
        new() { Notes = [note] };

    public static ConstraintResult WithPenalty(double penalty, string? note = null) =>
        new() { SoftPenalty = penalty, Notes = note != null ? [note] : [] };
}
