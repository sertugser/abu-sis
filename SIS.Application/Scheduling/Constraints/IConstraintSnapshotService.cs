using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Application.Scheduling.Constraints;

public interface IConstraintSnapshotService
{
    Task<ConstraintViolationSnapshot> TakeSnapshotAsync(int termId, SnapshotType type, string? userId, CancellationToken ct);
    Task<IReadOnlyList<SnapshotDto>> GetSnapshotsAsync(int termId, int limit, CancellationToken ct);
    Task<TrendDataDto> GetTrendDataAsync(int termId, CancellationToken ct);
    Task<SnapshotComparisonDto> CompareSnapshotsAsync(int id1, int id2, CancellationToken ct);
    Task<HeatmapDto> GetHeatmapAsync(int termId, CancellationToken ct);
    Task<BottleneckDto> GetBottlenecksAsync(int termId, CancellationToken ct);
}
