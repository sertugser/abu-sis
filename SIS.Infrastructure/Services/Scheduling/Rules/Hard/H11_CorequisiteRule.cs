using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.11 — H11 Eşkoşul Uyumu.
/// V2 encoder gerçek enforce'u yapmıyor (Plan 6.5'a ertelendi); predicate V1 mantığını port eder.
/// Kural: candidate'in CourseId'si bir corequisite çiftinin bir tarafıysa, candidate'in aynı
/// CourseId'sinden bir section'ı, çiftteki diğer CourseId'nin bir section'ı ile çakışmıyor
/// olmalı (öğrenci her ikisini de alabileceği bir kombinasyon var olmalı).
/// </summary>
public sealed class H11_CorequisiteRule : IRulePredicate
{
    public string Code => "H11";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        var snapshot = ctx.Snapshot;
        if (snapshot.Corequisites.Count == 0) return Array.Empty<RuleViolation>();

        foreach (var coreq in snapshot.Corequisites)
        {
            int? pairedCourseId = coreq.CourseId == c.CourseId
                ? coreq.RequiredCourseId
                : coreq.RequiredCourseId == c.CourseId ? coreq.CourseId : (int?)null;
            if (!pairedCourseId.HasValue) continue;

            var paired = snapshot.Sections
                .Where(s => s.CourseId == pairedCourseId.Value && s.Blocks.Count > 0).ToList();
            if (paired.Count == 0) continue;

            var candidateCourseSections = snapshot.Sections
                .Where(s => s.CourseId == c.CourseId && s.SectionId != c.SectionId && s.Blocks.Count > 0)
                .Append(c).Where(s => s.Blocks.Count > 0).ToList();

            var hasNonConflicting = candidateCourseSections.Any(a =>
                paired.Any(b => !AnyOverlap(a.Blocks, b.Blocks)));

            if (!hasNonConflicting)
            {
                return new[]
                {
                    new RuleViolation(
                        Code: Code, ConflictType: "CorequisiteConflict",
                        Message: $"Corequisite dersler ({c.CourseCode} ve {paired[0].CourseCode}) arasında çakışmasız bir şube kombinasyonu bulunmuyor.",
                        SectionId: c.SectionId)
                };
            }
        }

        return Array.Empty<RuleViolation>();
    }

    private static bool AnyOverlap(IReadOnlyList<RuntimeBlock> a, IReadOnlyList<RuntimeBlock> b)
    {
        foreach (var x in a)
            foreach (var y in b)
                if (x.Day == y.Day && x.StartTime < y.EndTime && x.EndTime > y.StartTime)
                    return true;
        return false;
    }
}
