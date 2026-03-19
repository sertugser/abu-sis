namespace SIS.Domain.Enums;

public enum ConflictOverlapType
{
    None,
    Partial,   // partial overlap - requires advisor + dept manager approval
    Full       // full overlap - blocked for first-time courses, approval needed for repeats
}
