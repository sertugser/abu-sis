using SIS.Domain.Enums;

namespace SIS.Application.Scheduling.Solver;

public sealed record ClassroomInput
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required int Capacity { get; init; }
    public required ClassroomType Type { get; init; }
}
