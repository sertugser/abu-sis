using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed class ConstraintSnapshotService : IConstraintSnapshotService
{
    private readonly AppDbContext _context;
    private readonly ISchedulingService _schedulingService;

    public ConstraintSnapshotService(
        AppDbContext context,
        ISchedulingService schedulingService)
    {
        _context = context;
        _schedulingService = schedulingService;
    }

    public async Task<ConstraintViolationSnapshot> TakeSnapshotAsync(
        int termId, SnapshotType type, string? userId, CancellationToken ct)
    {
        var breakdown = await _schedulingService.GetScoreBreakdownAsync(termId, ct);

        var snapshot = new ConstraintViolationSnapshot
        {
            TermId = termId,
            SnapshotAt = DateTime.UtcNow,
            TotalHardViolations = breakdown.TotalHardViolations,
            TotalSoftPenalty = breakdown.TotalSoftPenalty,
            IsFeasible = breakdown.Feasible,
            SnapshotType = type,
            CreatedByUserId = userId,
        };

        foreach (var c in breakdown.Constraints)
        {
            snapshot.Details.Add(new ConstraintViolationSnapshotDetail
            {
                ConstraintCode = c.Code,
                Category = c.Category,
                ViolationCount = c.ViolationCount,
                Penalty = c.RawPenalty,
                WeightedPenalty = c.WeightedPenalty,
                AffectedSectionCount = c.AffectedSectionCount,
            });
        }

        _context.ConstraintViolationSnapshots.Add(snapshot);
        await _context.SaveChangesAsync(ct);

        return snapshot;
    }

    public async Task<IReadOnlyList<SnapshotDto>> GetSnapshotsAsync(
        int termId, int limit, CancellationToken ct)
    {
        var snapshots = await _context.ConstraintViolationSnapshots
            .AsNoTracking()
            .Where(s => s.TermId == termId)
            .OrderByDescending(s => s.SnapshotAt)
            .Take(limit)
            .Select(s => new SnapshotDto(
                s.Id, s.TermId, s.SnapshotAt, s.SnapshotType.ToString(),
                s.TotalHardViolations, s.TotalSoftPenalty, s.IsFeasible))
            .ToListAsync(ct);

        return snapshots;
    }

    public async Task<TrendDataDto> GetTrendDataAsync(int termId, CancellationToken ct)
    {
        var snapshots = await _context.ConstraintViolationSnapshots
            .AsNoTracking()
            .Where(s => s.TermId == termId)
            .OrderBy(s => s.SnapshotAt)
            .ToListAsync(ct);

        var dataPoints = snapshots.Select(s => new TrendPointDto(
            s.SnapshotAt, s.SnapshotType.ToString(),
            s.TotalHardViolations, s.TotalSoftPenalty, s.IsFeasible
        )).ToList();

        var summary = BuildTrendSummary(snapshots);

        return new TrendDataDto(termId, dataPoints, summary);
    }

    public async Task<SnapshotComparisonDto> CompareSnapshotsAsync(
        int id1, int id2, CancellationToken ct)
    {
        var snap1 = await _context.ConstraintViolationSnapshots
            .AsNoTracking()
            .Include(s => s.Details)
            .FirstOrDefaultAsync(s => s.Id == id1, ct)
            ?? throw new InvalidOperationException($"Snapshot {id1} bulunamadi.");

        var snap2 = await _context.ConstraintViolationSnapshots
            .AsNoTracking()
            .Include(s => s.Details)
            .FirstOrDefaultAsync(s => s.Id == id2, ct)
            ?? throw new InvalidOperationException($"Snapshot {id2} bulunamadi.");

        var allCodes = snap1.Details.Select(d => d.ConstraintCode)
            .Union(snap2.Details.Select(d => d.ConstraintCode))
            .Distinct()
            .ToList();

        var items = new List<ComparisonItemDto>();
        int improved = 0, worsened = 0, unchanged = 0;

        foreach (var code in allCodes)
        {
            var d1 = snap1.Details.FirstOrDefault(d => d.ConstraintCode == code);
            var d2 = snap2.Details.FirstOrDefault(d => d.ConstraintCode == code);

            var vBefore = d1?.ViolationCount ?? 0;
            var vAfter = d2?.ViolationCount ?? 0;
            var pBefore = d1?.WeightedPenalty ?? 0;
            var pAfter = d2?.WeightedPenalty ?? 0;
            var vDelta = vAfter - vBefore;
            var pDelta = pAfter - pBefore;

            string change;
            if (vDelta < 0 || (vDelta == 0 && pDelta < 0))
            {
                change = "improved";
                improved++;
            }
            else if (vDelta > 0 || (vDelta == 0 && pDelta > 0))
            {
                change = "worsened";
                worsened++;
            }
            else
            {
                change = "unchanged";
                unchanged++;
            }

            items.Add(new ComparisonItemDto(
                code, d1?.Category ?? d2?.Category ?? "",
                vBefore, vAfter, vDelta,
                Math.Round(pBefore, 1), Math.Round(pAfter, 1), Math.Round(pDelta, 1),
                change));
        }

        return new SnapshotComparisonDto(
            ToDto(snap1), ToDto(snap2),
            items.OrderByDescending(i => Math.Abs(i.ViolationDelta)).ToList(),
            improved, worsened, unchanged);
    }

    public async Task<HeatmapDto> GetHeatmapAsync(int termId, CancellationToken ct)
    {
        var conflicts = await _schedulingService.GetConflictsAsync(termId, ct);

        var grouped = conflicts
            .Where(c => c.Day.HasValue && c.StartTime.HasValue)
            .GroupBy(c => c.ConflictType)
            .ToList();

        var rows = new List<HeatmapRowDto>();

        foreach (var group in grouped)
        {
            var cells = group
                .GroupBy(c => new { Day = (int)c.Day!.Value, SlotIndex = c.StartTime!.Value.Hour })
                .Select(g => new HeatmapCellDto(
                    g.Key.Day,
                    g.Key.SlotIndex,
                    g.Count(),
                    GetSeverity(g.Count())))
                .ToList();

            rows.Add(new HeatmapRowDto(group.Key, group.Key, cells));
        }

        return new HeatmapDto(rows);
    }

    public async Task<BottleneckDto> GetBottlenecksAsync(int termId, CancellationToken ct)
    {
        var breakdown = await _schedulingService.GetScoreBreakdownAsync(termId, ct);
        var conflicts = await _schedulingService.GetConflictsAsync(termId, ct);

        var totalValue = breakdown.Constraints.Sum(c =>
            c.Category == "Hard" ? c.ViolationCount : c.WeightedPenalty);

        var topConstraints = breakdown.Constraints
            .OrderByDescending(c => c.Category == "Hard" ? c.ViolationCount * 1000 : c.WeightedPenalty)
            .Take(5)
            .Select(c => new BottleneckConstraintDto(
                c.Code, c.Name,
                totalValue > 0
                    ? Math.Round((c.Category == "Hard" ? c.ViolationCount : c.WeightedPenalty) / totalValue * 100, 1)
                    : 0,
                c.ViolationCount))
            .ToList();

        // Top resources by conflict count
        var instructorConflicts = conflicts
            .Where(c => c.InstructorId.HasValue)
            .GroupBy(c => new { c.InstructorId, c.InstructorName })
            .Select(g => new BottleneckResourceDto("Instructor", g.Key.InstructorName ?? $"ID:{g.Key.InstructorId}", g.Count()))
            .OrderByDescending(r => r.ConflictCount)
            .Take(5)
            .ToList();

        var classroomConflicts = conflicts
            .Where(c => c.ClassroomId.HasValue)
            .GroupBy(c => new { c.ClassroomId, c.ClassroomName })
            .Select(g => new BottleneckResourceDto("Classroom", g.Key.ClassroomName ?? $"ID:{g.Key.ClassroomId}", g.Count()))
            .OrderByDescending(r => r.ConflictCount)
            .Take(5)
            .ToList();

        var topResources = instructorConflicts.Concat(classroomConflicts)
            .OrderByDescending(r => r.ConflictCount)
            .Take(5)
            .ToList();

        // Top time slots
        var dayNames = new[] { "Paz", "Pzt", "Sal", "Car", "Per", "Cum", "Cmt" };
        var topSlots = conflicts
            .Where(c => c.Day.HasValue && c.StartTime.HasValue)
            .GroupBy(c => new { Day = (int)c.Day!.Value, Hour = c.StartTime!.Value.Hour })
            .Select(g => new BottleneckSlotDto(
                dayNames.ElementAtOrDefault(g.Key.Day) ?? g.Key.Day.ToString(),
                $"{g.Key.Hour:D2}:00",
                g.Count()))
            .OrderByDescending(s => s.TotalViolations)
            .Take(5)
            .ToList();

        return new BottleneckDto(topConstraints, topResources, topSlots);
    }

    private static TrendSummaryDto BuildTrendSummary(List<ConstraintViolationSnapshot> snapshots)
    {
        if (snapshots.Count < 2)
            return new TrendSummaryDto("N/A", "N/A", "insufficient_data");

        var first = snapshots[0];
        var last = snapshots[^1];

        var hardDelta = last.TotalHardViolations - first.TotalHardViolations;
        var penaltyDelta = last.TotalSoftPenalty - first.TotalSoftPenalty;

        var hardReduction = first.TotalHardViolations > 0
            ? $"{Math.Round((double)-hardDelta / first.TotalHardViolations * 100, 1)}%"
            : "N/A";

        var penaltyReduction = first.TotalSoftPenalty > 0
            ? $"{Math.Round(-penaltyDelta / first.TotalSoftPenalty * 100, 1)}%"
            : "N/A";

        string trend;
        if (hardDelta < 0 || (hardDelta == 0 && penaltyDelta < 0))
            trend = "improving";
        else if (hardDelta > 0 || (hardDelta == 0 && penaltyDelta > 0))
            trend = "worsening";
        else
            trend = "stable";

        return new TrendSummaryDto(hardReduction, penaltyReduction, trend);
    }

    private static string GetSeverity(int count) => count switch
    {
        0 => "none",
        1 => "low",
        2 or 3 => "medium",
        _ => "high",
    };

    private static SnapshotDto ToDto(ConstraintViolationSnapshot s) =>
        new(s.Id, s.TermId, s.SnapshotAt, s.SnapshotType.ToString(),
            s.TotalHardViolations, s.TotalSoftPenalty, s.IsFeasible);
}
