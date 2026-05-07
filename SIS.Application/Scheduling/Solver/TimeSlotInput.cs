using System;
using SIS.Domain.Enums;

namespace SIS.Application.Scheduling.Solver;

public sealed record TimeSlotInput
{
    public required int Id { get; init; }
    public required DayOfWeek DayOfWeek { get; init; }
    public required int SlotOrder { get; init; }
    public required TimeOnly StartTime { get; init; }
    public required TimeOnly EndTime { get; init; }
    public required TimeSlotAllowedMode AllowedMode { get; init; }
}
