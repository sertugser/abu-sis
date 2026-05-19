using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.1 — H1 Derslik Çakışması.
/// V1 Plan 10 paralel-lab fix port: per-block ClassroomId üzerinden eşleşme, aynı section'ın
/// kendi paralel grup blokları çakışma sayılmaz (self-skip). V2 AddNoOverlap ile aynı semantik.
/// </summary>
public sealed class H1_ClassroomConflictRule : IRulePredicate
{
    public string Code => "H1";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        if (c.Blocks.Count == 0) return Array.Empty<RuleViolation>();

        var violations = new List<RuleViolation>();

        foreach (var cBlock in c.Blocks)
        {
            var candidateRoom = cBlock.ClassroomId;
            if (!candidateRoom.HasValue) continue;

            foreach (var existing in ctx.Snapshot.Sections)
            {
                // Self-skip: aynı section'ın paralel grup blokları farklı odalardadır.
                if (existing.SectionId == c.SectionId) continue;

                foreach (var eBlock in existing.Blocks)
                {
                    var existingRoom = eBlock.ClassroomId;
                    if (existingRoom != candidateRoom) continue;
                    if (cBlock.Day != eBlock.Day) continue;

                    // Zaman çakışması (yarı-açık aralık).
                    if (cBlock.StartTime < eBlock.EndTime && cBlock.EndTime > eBlock.StartTime)
                    {
                        var overlapStart = cBlock.StartTime > eBlock.StartTime ? cBlock.StartTime : eBlock.StartTime;
                        var overlapEnd = cBlock.EndTime < eBlock.EndTime ? cBlock.EndTime : eBlock.EndTime;

                        violations.Add(new RuleViolation(
                            Code: Code, ConflictType: "ClassroomConflict",
                            Message: $"Derslik '{cBlock.ClassroomName}' aynı zaman aralığında '{existing.CourseCode}' section'u ile çakışıyor.",
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
