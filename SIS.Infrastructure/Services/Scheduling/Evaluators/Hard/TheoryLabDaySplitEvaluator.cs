using SIS.Application.Scheduling.Constraints;

namespace SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;

/// <summary>
/// H16 — Teori/Lab gün ayrımı (revize 2026-05).
/// Eski kural: total > threshold ise theory + lab AYRI günlerde zorunlu.
/// Yeni kural (CandidateGenerator.BuildMultiLabCombinations ile aynı): theory + lab
/// AYNI gün olabilir ama lab teoriden SONRA olmalı (lab.StartTime >= theory.EndTime).
/// Lab > 2 saat ise blok blok farklı günlere zaten dağıtılır (CandidateGenerator).
/// Bu evaluator artık sadece "lab teoriden ÖNCE aynı günde" durumunu yakalıyor.
/// </summary>
public sealed class TheoryLabDaySplitEvaluator : IConstraintEvaluator
{
    public string Code => "H16";
    public string Name => "Teori-Lab Sira Kontrolu";
    public ConstraintCategory Category => ConstraintCategory.Hard;

    public ConstraintResult Evaluate(IConstraintContext ctx)
    {
        var context = (SchedulingConstraintContext)ctx;
        var c = context.Candidate;

        if (c.TheoryHours <= 0 || c.LabHours <= 0)
            return ConstraintResult.Ok();

        if (c.TimeBlocks.Count == 0)
            return ConstraintResult.Ok();

        // TimeBlock IsTheory ayrımı taşımıyor; theory bloğunu süre ile tahmin et:
        //   theory bloğu = section.TheoryHours saat süren (Day, StartTime, EndTime) penceresi.
        //   Lab blokları = kalanlar (paralel split aynı pencerede birden fazla classroom).
        var theoryDurationHours = c.TheoryHours;
        var theoryWindow = c.TimeBlocks
            .GroupBy(b => (b.Day, b.StartTime, b.EndTime))
            .Where(g => (int)System.Math.Round((g.Key.EndTime - g.Key.StartTime).TotalHours) == theoryDurationHours)
            .Select(g => g.Key)
            .FirstOrDefault();

        // Theory penceresi tespit edilemediyse (örn. blok süreleri ayırt edilemiyor) skip.
        if (theoryWindow.Equals(default((System.DayOfWeek, System.TimeOnly, System.TimeOnly))))
            return ConstraintResult.Ok();

        // Aynı gündeki lab blokları (theory penceresi DIŞINDAKİ tüm bloklar lab):
        var sameDayLabBlocks = c.TimeBlocks
            .Where(b => b.Day == theoryWindow.Day
                && !(b.StartTime == theoryWindow.StartTime && b.EndTime == theoryWindow.EndTime))
            .ToList();

        // Aynı günde lab blokları varsa, hepsi theory'den SONRA başlamalı.
        foreach (var lab in sameDayLabBlocks)
        {
            if (lab.StartTime < theoryWindow.EndTime)
            {
                return ConstraintResult.WithConflict(SchedulingHelpers.BuildConflict(
                    c,
                    "TheoryLabDaySplitConflict",
                    $"Karma ders '{c.CourseCode}': lab bloku ({lab.Day} {lab.StartTime:HH\\:mm}) " +
                    $"ayni gundeki teoriden ({theoryWindow.StartTime:HH\\:mm}-{theoryWindow.EndTime:HH\\:mm}) ONCE basliyor. " +
                    $"Lab teoriden sonra olmali.",
                    lab,
                    null));
            }
        }

        return ConstraintResult.Ok();
    }
}
