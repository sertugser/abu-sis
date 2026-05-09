using Google.OrTools.Sat;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H6 — Theory-Lab Component Conflict. İki davranış kapsanır:
/// <list type="bullet">
///   <item>Same-section: Aynı section'ın teori ve lab interval'ları çakışamaz (öğrenci aynı anda iki yerde olamaz).</item>
///   <item>Cross-section (Plan 9 Task 11): Aynı CourseId'nin farklı section'larında theory ve lab
///     bileşenleri çakışamaz. v1 <c>CourseComponentEvaluator</c> paritesi — split lecture-only +
///     lab-only section pattern'inde aynı dersin lecture'ına ve lab'ına kayıtlı öğrenci ikisini
///     ayrı saatlerde almak zorundadır.</item>
/// </list>
/// CP-SAT model: AddNoOverlap optional intervals üzerinde. Multi-block lab desteği için
/// <see cref="DecisionVariables.GetLabIntervals"/> plural accessor ile her block'a ayrı kısıt eklenir.
/// </summary>
public sealed class TheoryLabComponentEncoder : IConstraintEncoder
{
    public string Code => "H6";
    public bool IsHard => true;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        EncodeSameSection(ctx);
        EncodeCrossSection(ctx);
    }

    private void EncodeSameSection(EncoderContext ctx)
    {
        foreach (var section in ctx.Request.Sections)
        {
            if (section.TheoryHours <= 0 || section.LabHours <= 0)
                continue;

            var theory = ctx.Variables.GetLectureInterval(section.Id);
            var lab = ctx.Variables.GetLabInterval(section.Id);
            if (theory == null || lab == null)
                continue;

            ctx.Model.AddNoOverlap(new[] { theory, lab });
            ctx.Assumptions.Register(Code, primarySectionId: section.Id, secondarySectionId: null);
        }
    }

    /// <summary>
    /// Plan 9 Task 11 — v1 <c>CourseComponentEvaluator</c> paritesi. Aynı CourseId'nin
    /// farklı section'larında (sectionA.theory) vs (sectionB.lab) çakışması yasak.
    /// Multi-block lab desteği için <see cref="DecisionVariables.GetLabIntervals"/> plural
    /// accessor'ından her block için ayrı AddNoOverlap eklenir.
    /// </summary>
    private void EncodeCrossSection(EncoderContext ctx)
    {
        var byCourse = ctx.Request.Sections.GroupBy(s => s.CourseId);

        foreach (var courseGroup in byCourse)
        {
            var sectionsInCourse = courseGroup.ToList();
            if (sectionsInCourse.Count < 2) continue;

            for (int i = 0; i < sectionsInCourse.Count; i++)
            {
                for (int j = 0; j < sectionsInCourse.Count; j++)
                {
                    if (i == j) continue;
                    var a = sectionsInCourse[i];
                    var b = sectionsInCourse[j];

                    if (a.TheoryHours <= 0 || b.LabHours <= 0) continue;

                    var theoryA = ctx.Variables.GetLectureInterval(a.Id);
                    if (theoryA == null) continue;

                    IReadOnlyList<IntervalVar> labIntervalsB;
                    try
                    {
                        labIntervalsB = ctx.Variables.GetLabIntervals(b.Id);
                    }
                    catch (KeyNotFoundException)
                    {
                        continue;
                    }

                    foreach (var labIvB in labIntervalsB)
                    {
                        ctx.Model.AddNoOverlap(new[] { theoryA, labIvB });
                    }
                    ctx.Assumptions.Register(Code, primarySectionId: a.Id, secondarySectionId: b.Id);
                }
            }
        }
    }
}
