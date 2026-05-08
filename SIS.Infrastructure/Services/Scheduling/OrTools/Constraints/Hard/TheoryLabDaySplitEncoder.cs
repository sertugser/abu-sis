using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H12 — Theory-Lab Day Split. Section'ın teori+lab toplam saati eşiği aşıyorsa,
/// teori ve lab farklı günlerde olmalı (öğrenci/hoca yorulmasın).
///
/// Eşik (splitThreshold) SystemConfig.Schedule.TheoryLabSplitThreshold'tan okunur (default 4).
/// Encoder.Encode sync olduğu için eşik constructor'a enjekte edilir; DI factory ile config'ten
/// okuma SchedulingServiceCollectionExtensions'ta yapılır.
///
/// CP-SAT model: <see cref="DecisionVariables.GetOrCreateComponentDayVar"/> her component için
/// (lecture/lab) gün IntVar'ını döndürür (idempotent, dayPerSlot cache paylaşımlı). Sonra
/// theoryDay != labDay constraint'i assumption literal ile gated eklenir.
/// </summary>
public sealed class TheoryLabDaySplitEncoder : IConstraintEncoder
{
    public string Code => "H12";
    public bool IsHard => true;

    private readonly int _splitThreshold;

    public TheoryLabDaySplitEncoder(int splitThreshold = 4)
    {
        if (splitThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(splitThreshold),
                splitThreshold, "splitThreshold must be positive.");
        _splitThreshold = splitThreshold;
    }

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        if (ctx.Request.TimeSlots.Count == 0)
            return;  // no slots = no day data

        foreach (var section in ctx.Request.Sections)
        {
            // Skip sections without both components
            if (section.TheoryHours <= 0 || section.LabHours <= 0)
                continue;

            // Skip sections that don't exceed threshold
            var total = section.TheoryHours + section.LabHours;
            if (total <= _splitThreshold)
                continue;

            var theoryDay = ctx.Variables.GetOrCreateComponentDayVar(
                section.Id, ComponentType.Lecture, ctx.Request.TimeSlots);
            var labDay = ctx.Variables.GetOrCreateComponentDayVar(
                section.Id, ComponentType.Lab, ctx.Request.TimeSlots);

            // Constraint: theoryDay != labDay (gated by assumption literal)
            var literal = ctx.Assumptions.Register(Code, primarySectionId: section.Id, secondarySectionId: null);
            ctx.Model.Add(theoryDay != labDay).OnlyEnforceIf(literal);
        }
    }
}
