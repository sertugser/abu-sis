using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.10 — H10 Fakülte Derslik Ataması.
/// Section'ın bölümünden türetilen fakülte ile atanmış derslik bu dönem için
/// ClassroomTermAssignments tablosunda eşleşmeli. V1 evaluator ile birebir port.
/// </summary>
public sealed class H10_FacultyClassroomRule : ISoftRulePredicate
{
    public string Code => "H10";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    // Soft modda (admin "Tercihli") penalty: ihlal başına solver SoftThresholds.H10Weight default'u (5).
    // RuleEngine sonra def.Weight (kullanıcı ağırlığı) ile çarpar — H8/DIST hybrid pattern.
    private const double SoftPenaltyPerViolation = 5;

    public SoftRuleResult CalculatePenalty(RuleContext ctx)
    {
        var violations = Evaluate(ctx);
        if (violations.Count == 0) return SoftRuleResult.Zero;
        var notes = violations.Take(5).Select(v => v.Message).ToList();
        var sections = violations.Select(v => v.SectionId).Distinct().Count();
        return new SoftRuleResult(violations.Count * SoftPenaltyPerViolation, violations.Count, sections, notes);
    }

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var snapshot = ctx.Snapshot;

        if (c.Blocks.Count == 0 || !c.DepartmentId.HasValue
            || snapshot.ClassroomAssignments.Count == 0)
            return Array.Empty<RuleViolation>();

        var primary = c.Blocks[0];
        if (!primary.ClassroomId.HasValue)
            return Array.Empty<RuleViolation>();

        if (!snapshot.DeptFacultyMap.TryGetValue(c.DepartmentId.Value, out var candidateFacultyId)
            || candidateFacultyId <= 0)
            return Array.Empty<RuleViolation>();

        var hasAssignment = snapshot.ClassroomAssignments
            .Any(cta => cta.ClassroomId == primary.ClassroomId.Value && cta.FacultyId == candidateFacultyId);

        if (!hasAssignment)
        {
            return new[]
            {
                new RuleViolation(
                    Code: Code, ConflictType: "FacultyClassroomConflict",
                    Message: $"Derslik '{primary.ClassroomName}' bu dönemde ilgili fakülteye atanmamış.",
                    SectionId: c.SectionId)
            };
        }

        return Array.Empty<RuleViolation>();
    }
}
