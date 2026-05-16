using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.2 — H2 Hoca Çakışması. Aynı InstructorId'li iki section aynı zaman aralığında
/// olamaz. V1 InstructorConflictEvaluator port; EvaluateAllExisting filteredSnapshot pattern
/// RuleEngine'a taşındığı için burada self-skip yapmak gereksiz (snapshot zaten candidate'i
/// hariç tutar) — ama defansif olarak SectionId eşleşmesini atlıyoruz.
/// </summary>
public sealed class H2_InstructorConflictRule : IRulePredicate
{
    public string Code => "H2";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        if (!c.InstructorId.HasValue || c.Blocks.Count == 0)
            return Array.Empty<RuleViolation>();

        var violations = new List<RuleViolation>();

        foreach (var existing in ctx.Snapshot.Sections)
        {
            if (existing.SectionId == c.SectionId) continue;
            if (existing.InstructorId != c.InstructorId) continue;

            foreach (var cBlock in c.Blocks)
            {
                foreach (var eBlock in existing.Blocks)
                {
                    if (cBlock.Day != eBlock.Day) continue;
                    if (cBlock.StartTime < eBlock.EndTime && cBlock.EndTime > eBlock.StartTime)
                    {
                        var overlapStart = cBlock.StartTime > eBlock.StartTime ? cBlock.StartTime : eBlock.StartTime;
                        var overlapEnd = cBlock.EndTime < eBlock.EndTime ? cBlock.EndTime : eBlock.EndTime;

                        violations.Add(new RuleViolation(
                            Code: Code, ConflictType: "InstructorConflict",
                            Message: $"Öğretim üyesi '{c.InstructorName}' aynı zaman aralığında '{existing.CourseCode}' section'u ile çakışıyor.",
                            SectionId: c.SectionId,
                            RelatedSectionId: existing.SectionId,
                            Day: cBlock.Day,
                            StartTime: overlapStart,
                            EndTime: overlapEnd));
                    }
                }
            }
        }

        return violations;
    }
}
