using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.5 — H5 Müfredat Çakışması.
/// 3 mod (V1 ile parite): "enrollment", "cohort", "curriculum" (default).
///   - enrollment: <c>Snapshot.EnrollmentConflicts.HardConflictPairs</c> üzerinden
///   - cohort: <c>Snapshot.CohortConflictPairs</c> üzerinden
///   - curriculum: SuggestedSemestersByDepartment + AccessibleDepartmentIds üzerinden
///     aynı (Dept, Year) cohort'unda farklı CourseId'ler.
/// </summary>
public sealed class H5_CurriculumConflictRule : IRulePredicate
{
    public string Code => "H5";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var snapshot = ctx.Snapshot;
        var violations = new List<RuleViolation>();

        // Enrollment mode
        if (snapshot.EnrollmentConflicts != null)
        {
            foreach (var existing in snapshot.Sections)
            {
                if (existing.SectionId == c.SectionId) continue;
                if (existing.CourseId == c.CourseId) continue;

                var pair = c.SectionId < existing.SectionId
                    ? (c.SectionId, existing.SectionId)
                    : (existing.SectionId, c.SectionId);
                if (!snapshot.EnrollmentConflicts.HardConflictPairs.Contains(pair)) continue;

                AddOverlapViolations(c, existing, "CurriculumConflict",
                    $"'{c.CourseCode}' ile '{existing.CourseCode}' derslerini ortak alan öğrenciler var, zaman çakışması.",
                    violations);
            }
            return violations;
        }

        // Cohort mode
        if (snapshot.CohortConflictPairs.Count > 0)
        {
            foreach (var existing in snapshot.Sections)
            {
                if (existing.SectionId == c.SectionId) continue;
                if (existing.CourseId == c.CourseId) continue;

                var pair = c.SectionId < existing.SectionId
                    ? (c.SectionId, existing.SectionId)
                    : (existing.SectionId, c.SectionId);
                if (!snapshot.CohortConflictPairs.Contains(pair)) continue;

                AddOverlapViolations(c, existing, "CurriculumConflict",
                    $"Aynı kohorttaki '{c.CourseCode}' ile '{existing.CourseCode}' dersleri çakışıyor.",
                    violations);
            }
            return violations;
        }

        // Curriculum mode (default)
        foreach (var existing in snapshot.Sections)
        {
            if (existing.SectionId == c.SectionId) continue;
            if (existing.CourseId == c.CourseId) continue;

            if (!ShareCurriculumDepartment(c, existing)) continue;

            AddOverlapViolations(c, existing, "CurriculumConflict",
                $"Aynı bölüm/yıl öğrencilerinin alabileceği '{c.CourseCode}' ile '{existing.CourseCode}' dersleri aynı zaman aralığında çakışıyor.",
                violations);
        }

        return violations;
    }

    private static bool ShareCurriculumDepartment(RuntimeCandidate a, RuntimeCandidate b)
    {
        // V1 TryGetSharedCurriculumDepartment portu: aynı dept'te aynı SuggestedSemester ve
        // her ikisi de bu dept müfredatında erişilebilir.
        foreach (var deptId in a.AccessibleDepartmentIds)
        {
            if (!b.AccessibleDepartmentIds.Contains(deptId)) continue;
            if (a.SuggestedSemestersByDepartment.TryGetValue(deptId, out var aSem)
                && b.SuggestedSemestersByDepartment.TryGetValue(deptId, out var bSem)
                && aSem == bSem)
                return true;
        }
        return false;
    }

    private static void AddOverlapViolations(
        RuntimeCandidate c, RuntimeCandidate existing, string conflictType, string message,
        List<RuleViolation> bag)
    {
        foreach (var a in c.Blocks)
        foreach (var b in existing.Blocks)
        {
            if (a.Day != b.Day) continue;
            if (a.StartTime < b.EndTime && a.EndTime > b.StartTime)
            {
                var ovS = a.StartTime > b.StartTime ? a.StartTime : b.StartTime;
                var ovE = a.EndTime < b.EndTime ? a.EndTime : b.EndTime;
                bag.Add(new RuleViolation(
                    Code: "H5", ConflictType: conflictType, Message: message,
                    SectionId: c.SectionId, RelatedSectionId: existing.SectionId,
                    Day: a.Day, StartTime: ovS, EndTime: ovE));
            }
        }
    }
}
