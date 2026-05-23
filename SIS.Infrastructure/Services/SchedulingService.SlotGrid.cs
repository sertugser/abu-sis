using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.DTOs;

namespace SIS.Infrastructure.Services;

/// <summary>
/// SchedulingService — dönem ders saat grid'i (zaman ekseninin tek kaynağı).
/// Derslik doluluk ekranındaki tüm saat odaklı görünümler (Boş Ara formu, haftalık
/// ızgara) ve haftalık doluluk oranı tek bir slot tanımından beslenir; böylece
/// önceki sabit 08-17 / 5×10 varsayımları ve 50 dk slot kaymasından kaynaklanan
/// tutarsızlıklar ortadan kalkar.
/// </summary>
public partial class SchedulingService
{
    /// <summary>Bir slotun gün-bağımsız zaman penceresi.</summary>
    public sealed record SlotWindow(int SlotOrder, TimeOnly Start, TimeOnly End, string Label);

    /// <summary>
    /// Dönemin slot tanımı: SlotOrder bazında tekil pencereler, ders günü indeksleri (0-4)
    /// ve günlük slot sayısı. Doluluk paydası = WorkDayIndices.Count × SlotsPerDay.
    /// </summary>
    public sealed record TermSlotGrid(
        IReadOnlyList<SlotWindow> Slots,
        IReadOnlyList<int> WorkDayIndices,
        int SlotsPerDay);

    private async Task<TermSlotGrid> LoadTermSlotGridAsync(int termId, CancellationToken cancellationToken)
    {
        var timeSlots = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .OrderBy(ts => ts.DayOfWeek).ThenBy(ts => ts.SlotOrder)
            .ToListAsync(cancellationToken);

        var slots = timeSlots
            .GroupBy(ts => ts.SlotOrder)
            .Select(g => g.First())
            .OrderBy(ts => ts.SlotOrder)
            .Select(ts => new SlotWindow(
                ts.SlotOrder,
                ts.StartTime,
                ts.EndTime,
                $"{ts.StartTime:HH:mm}-{ts.EndTime:HH:mm}"))
            .ToList();

        var workDayIndices = timeSlots
            .Select(ts => (int)ts.DayOfWeek - 1)
            .Where(d => d is >= 0 and <= 4)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        return new TermSlotGrid(slots, workDayIndices, slots.Count);
    }

    private static List<TimeSlotSummaryDto> ProjectSlotSummaries(TermSlotGrid grid) =>
        grid.Slots
            .Select(s => new TimeSlotSummaryDto
            {
                SlotOrder = s.SlotOrder,
                StartTime = s.Start.ToString("HH:mm"),
                EndTime = s.End.ToString("HH:mm"),
                Label = s.Label
            })
            .ToList();

    /// <summary>
    /// Bir dersliğin bloklarından haftalık doluluk oranını (%) hesaplar.
    /// Bir blok bir slot penceresiyle çakışıyorsa o (gün, slot) çifti dolu sayılır.
    /// Tek kaynak: dashboard ve Boş Ara aynı formülü kullanır.
    /// </summary>
    private static double ComputeWeeklyOccupancyRate(
        IEnumerable<(int Day, TimeOnly Start, TimeOnly End)> blocks,
        TermSlotGrid grid)
    {
        var denominator = grid.WorkDayIndices.Count * grid.SlotsPerDay;
        if (denominator == 0) return 0;

        var occupied = new HashSet<(int Day, int SlotOrder)>();
        foreach (var block in blocks)
        {
            var dayIndex = block.Day - 1;
            if (dayIndex is < 0 or > 4 || !grid.WorkDayIndices.Contains(dayIndex))
                continue;

            foreach (var slot in grid.Slots)
            {
                if (block.Start < slot.End && slot.Start < block.End)
                    occupied.Add((dayIndex, slot.SlotOrder));
            }
        }

        return Math.Round((double)occupied.Count / denominator * 100, MidpointRounding.AwayFromZero);
    }
}
