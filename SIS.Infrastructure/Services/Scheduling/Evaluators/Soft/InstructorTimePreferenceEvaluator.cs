using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;

public sealed class InstructorTimePreferenceEvaluator : IConstraintEvaluator
{
    public string Code => "IP1";
    public string Name => "Öğretim Üyesi Zaman Tercihi";
    public ConstraintCategory Category => ConstraintCategory.Soft;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;

        if (!c.InstructorId.HasValue || c.TimeBlocks.Count == 0)
            return ConstraintResult.Ok();

        var preferences = context.Snapshot.InstructorPreferences?
            .Where(p => p.InstructorId == c.InstructorId.Value
                && p.PreferenceType == PreferenceType.TimeSlotPreference
                && p.IsApproved)
            .ToList();

        if (preferences == null || preferences.Count == 0)
            return ConstraintResult.Ok();

        double totalPenalty = 0;
        var conflicts = new List<SchedulingConflictDto>();

        foreach (var pref in preferences)
        {
            foreach (var block in c.TimeBlocks)
            {
                // DayOfWeek in preference: 0=Monday..5=Saturday matching DayOfWeek enum
                if (pref.DayOfWeek.HasValue && (int)block.Day != pref.DayOfWeek.Value)
                    continue;

                bool overlaps = true;
                if (pref.TimeSlotStart.HasValue && pref.TimeSlotEnd.HasValue)
                    overlaps = block.StartTime < pref.TimeSlotEnd.Value && block.EndTime > pref.TimeSlotStart.Value;

                if (!overlaps) continue;

                switch (pref.PreferenceLevel)
                {
                    case PreferenceLevel.Unavailable:
                        conflicts.Add(SchedulingHelpers.BuildConflict(c, "InstructorTimePreference",
                            $"Öğretim üyesi bu zaman diliminde müsait değil.", block, null));
                        break;
                    case PreferenceLevel.StronglyDiscouraged:
                        totalPenalty += 30;
                        break;
                    case PreferenceLevel.Discouraged:
                        totalPenalty += 10;
                        break;
                    case PreferenceLevel.StronglyPreferred:
                        totalPenalty -= 20;
                        break;
                    case PreferenceLevel.Preferred:
                        totalPenalty -= 5;
                        break;
                }
            }
        }

        if (conflicts.Count > 0)
            return ConstraintResult.WithConflicts(conflicts);

        if (totalPenalty != 0)
        {
            var note = totalPenalty < 0
                ? $"IP1: Tercih edilen zaman diliminde ders var (bonus: {totalPenalty})"
                : $"IP1: Tercih edilmeyen zaman diliminde ders var (ceza: {totalPenalty})";
            return ConstraintResult.WithPenalty(totalPenalty, note);
        }

        return ConstraintResult.Ok();
    }
}
