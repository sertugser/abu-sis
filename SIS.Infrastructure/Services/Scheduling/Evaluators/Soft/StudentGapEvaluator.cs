using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;

internal sealed class StudentGapEvaluator : IConstraintEvaluator
{
    public string Code => "S7";
    public string Name => "Öğrenci Boşluk Minimizasyonu";
    public ConstraintCategory Category => ConstraintCategory.Soft;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;
        var config = context.Config;

        if (!config.S7Enabled || c.TimeBlocks.Count == 0
            || !c.DepartmentId.HasValue || !c.SuggestedSemester.HasValue)
            return ConstraintResult.Ok();

        var candidateYear = (c.SuggestedSemester.Value - 1) / 2;
        var sameDeptYearSections = context.Snapshot.Sections
            .Where(s => s.DepartmentId == c.DepartmentId
                && s.SuggestedSemester.HasValue
                && (s.SuggestedSemester.Value - 1) / 2 == candidateYear
                && s.TimeBlocks.Count > 0)
            .ToList();

        var combinedSlots = sameDeptYearSections
            .SelectMany(s => s.TimeBlocks.Select(slot => new { s.CourseCode, Slot = slot }))
            .Concat(c.TimeBlocks.Select(slot => new { c.CourseCode, Slot = slot }))
            .GroupBy(x => x.Slot.Day);

        double totalPenalty = 0;
        var notes = new List<string>();

        foreach (var dayGroup in combinedSlots)
        {
            var sorted = dayGroup.OrderBy(x => x.Slot.StartTime).ToList();
            if (sorted.Count < 2) continue;

            for (int idx = 1; idx < sorted.Count; idx++)
            {
                var gapHours = (sorted[idx].Slot.StartTime - sorted[idx - 1].Slot.EndTime).TotalHours;
                if (gapHours >= config.S7MaxGapSlots)
                {
                    totalPenalty += (gapHours - config.S7MaxGapSlots + 1) * 3;
                    var dayName = SchedulingHelpers.GetDayOfWeekName(dayGroup.Key);
                    notes.Add($"S7 Uyarı: {dayName} günü '{sorted[idx - 1].CourseCode}' ile '{sorted[idx].CourseCode}' arasında {gapHours:0.#} saatlik boşluk var.");
                }
            }
        }

        if (totalPenalty > 0)
            return new ConstraintResult { SoftPenalty = totalPenalty, Notes = notes };

        return ConstraintResult.Ok();
    }
}
