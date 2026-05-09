using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H5 — Curriculum Conflict (Plan 6 real impl).
/// CP-SAT model: dispatched on SolverRequest.CurriculumConflictMode.
///   - "curriculum" (default): aynı (DepartmentId, Year) cohort'undaki farklı course'ların
///     intervals'i çakışmasın. AddNoOverlap per group.
///   - "cohort": SolverRequest.CohortConflictPairs (v1 CohortBuilder portu) → her pair için
///     AddNoOverlap on [intervalA, intervalB]. Plan 6 Task 5'te aktive edilir.
///   - "enrollment": Plan 6.5'a ertelendi.
/// 1 assumption per group (curriculum) veya per pair (cohort).
/// Same-course sections (H8 SameCourseSection handles them) are skipped to avoid duplication.
/// </summary>
public sealed class CurriculumConflictEncoder : IConstraintEncoder
{
    public string Code => "H5";
    public bool IsHard => true;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;

        // Plan 10 — Category toggle: admin H5'i "Tercihli" (Soft) yaparsa hard ban skip;
        // CurriculumConflictSoftEncoder devreye girer (penalty mod). Default Hard.
        var category = ctx.Request.ConstraintCategories.GetValueOrDefault(Code, "Hard");
        if (!string.Equals(category, "Hard", StringComparison.OrdinalIgnoreCase))
            return;

        switch (ctx.Request.CurriculumConflictMode)
        {
            case "cohort":
                EncodeCohortMode(ctx);
                break;
            case "enrollment":
                // Plan 6.5'a ertelendi
                break;
            default:
                EncodeCurriculumMode(ctx);
                break;
        }
    }

    private void EncodeCurriculumMode(EncoderContext ctx)
    {
        // Group sections by (DepartmentId, Year). Skip null department or year.
        var groups = ctx.Request.Sections
            .Where(s => s.DepartmentId.HasValue && s.Year.HasValue)
            .GroupBy(s => (DeptId: s.DepartmentId!.Value, Year: s.Year!.Value));

        foreach (var group in groups)
        {
            // Within a group: collect intervals from sections of DIFFERENT courses.
            // Same-course sections handled by H8 (SameCourseSection); skip to avoid duplication.
            var byCourse = group.GroupBy(s => s.CourseId).ToList();
            if (byCourse.Count < 2) continue; // need at least 2 different courses to conflict

            var intervals = new List<IntervalVar>();
            foreach (var courseGroup in byCourse)
            {
                foreach (var section in courseGroup)
                {
                    if (section.TheoryHours > 0)
                    {
                        var iv = ctx.Variables.GetLectureInterval(section.Id);
                        if (iv != null) intervals.Add(iv);
                    }
                    if (section.LabHours > 0)
                    {
                        var iv = ctx.Variables.GetLabInterval(section.Id);
                        if (iv != null) intervals.Add(iv);
                    }
                }
            }

            if (intervals.Count < 2) continue;

            ctx.Model.AddNoOverlap(intervals);
            ctx.Assumptions.Register(Code,
                primarySectionId: group.Key.DeptId,
                secondarySectionId: group.Key.Year);
        }
    }

    private void EncodeCohortMode(EncoderContext ctx)
    {
        if (ctx.Request.CohortConflictPairs.Count == 0) return;

        // Pre-build dictionary: sectionId → SectionInput for fast lookup
        var sectionDict = ctx.Request.Sections.ToDictionary(s => s.Id);

        foreach (var (a, b) in ctx.Request.CohortConflictPairs)
        {
            if (!sectionDict.TryGetValue(a, out var sectionA)) continue;
            if (!sectionDict.TryGetValue(b, out var sectionB)) continue;

            var intervals = new List<IntervalVar>();
            AppendSectionIntervals(ctx, sectionA, intervals);
            AppendSectionIntervals(ctx, sectionB, intervals);

            if (intervals.Count < 2) continue;

            ctx.Model.AddNoOverlap(intervals);
            ctx.Assumptions.Register(Code,
                primarySectionId: a,
                secondarySectionId: b);
        }
    }

    private static void AppendSectionIntervals(EncoderContext ctx, SectionInput section,
        List<IntervalVar> intervals)
    {
        if (section.TheoryHours > 0)
        {
            var iv = ctx.Variables.GetLectureInterval(section.Id);
            if (iv != null) intervals.Add(iv);
        }
        if (section.LabHours > 0)
        {
            var iv = ctx.Variables.GetLabInterval(section.Id);
            if (iv != null) intervals.Add(iv);
        }
    }
}
