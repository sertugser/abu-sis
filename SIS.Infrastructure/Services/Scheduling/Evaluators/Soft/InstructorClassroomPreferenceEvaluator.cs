using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;

public sealed class InstructorClassroomPreferenceEvaluator : IConstraintEvaluator
{
    public string Code => "IP2";
    public string Name => "Öğretim Üyesi Derslik Tercihi";
    public ConstraintCategory Category => ConstraintCategory.Soft;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;

        if (!c.InstructorId.HasValue || !c.ClassroomId.HasValue)
            return ConstraintResult.Ok();

        var preferences = context.Snapshot.InstructorPreferences?
            .Where(p => p.InstructorId == c.InstructorId.Value
                && p.PreferenceType == PreferenceType.ClassroomPreference
                && p.IsApproved)
            .ToList();

        if (preferences == null || preferences.Count == 0)
            return ConstraintResult.Ok();

        double totalPenalty = 0;

        foreach (var pref in preferences)
        {
            bool matches = false;

            // Check by specific classroom
            if (pref.ClassroomId.HasValue)
                matches = c.ClassroomId == pref.ClassroomId.Value;

            // Check by building name
            if (!matches && !string.IsNullOrEmpty(pref.BuildingName))
            {
                // ClassroomName format is typically "BuildingName-RoomNumber"
                matches = c.ClassroomName.StartsWith(pref.BuildingName, StringComparison.OrdinalIgnoreCase);
            }

            if (!matches) continue;

            switch (pref.PreferenceLevel)
            {
                case PreferenceLevel.Unavailable:
                    totalPenalty += 50;
                    break;
                case PreferenceLevel.StronglyDiscouraged:
                    totalPenalty += 20;
                    break;
                case PreferenceLevel.Discouraged:
                    totalPenalty += 8;
                    break;
                case PreferenceLevel.StronglyPreferred:
                    totalPenalty -= 15;
                    break;
                case PreferenceLevel.Preferred:
                    totalPenalty -= 5;
                    break;
            }
        }

        if (totalPenalty != 0)
        {
            var note = totalPenalty < 0
                ? $"IP2: Tercih edilen derslikte ders var (bonus: {totalPenalty})"
                : $"IP2: Tercih edilmeyen derslikte ders var (ceza: {totalPenalty})";
            return ConstraintResult.WithPenalty(totalPenalty, note);
        }

        return ConstraintResult.Ok();
    }
}
