using System.Collections.Generic;

namespace SIS.Application.Scheduling.Solver;

public sealed record SolverRequest
{
    public required int TermId { get; init; }
    public required int FacultyId { get; init; }
    public int? DepartmentId { get; init; }
    public int? Seed { get; init; }
    public IReadOnlyList<SectionInput> Sections { get; init; } = [];
    public IReadOnlyList<ClassroomInput> Classrooms { get; init; } = [];
    public IReadOnlyList<TimeSlotInput> TimeSlots { get; init; } = [];
    public IReadOnlyDictionary<string, bool> ConstraintFlags { get; init; } =
        new Dictionary<string, bool>();
}
