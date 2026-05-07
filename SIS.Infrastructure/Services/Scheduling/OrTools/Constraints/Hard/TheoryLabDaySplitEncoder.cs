using Google.OrTools.Sat;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H12 — Theory-Lab Day Split. Section'ın teori+lab toplam saati eşiği aşıyorsa,
/// teori ve lab farklı günlerde olmalı (öğrenci/hoca yorulmasın).
///
/// Eşik (splitThreshold) SystemConfig.Schedule.TheoryLabSplitThreshold'tan okunur (default 4).
/// Encoder.Encode sync olduğu için eşik constructor'a enjekte edilir; DI factory ile config'ten
/// okuma SchedulingServiceCollectionExtensions'ta yapılır.
///
/// CP-SAT model: Day-of-slot mapping için pre-computed array oluşturulur, AddElement constraint
/// ile theory/lab interval'ının startSlot indeksi üzerinden gün hesaplanır, sonra
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

        // Pre-compute slot index → day-of-week int array for AddElement
        var timeSlots = ctx.Request.TimeSlots;
        if (timeSlots.Count == 0)
            return;  // no slots = no day data

        var dayPerSlot = new long[timeSlots.Count];
        for (int i = 0; i < timeSlots.Count; i++)
            dayPerSlot[i] = (long)timeSlots[i].DayOfWeek;

        foreach (var section in ctx.Request.Sections)
        {
            // Skip sections without both components
            if (section.TheoryHours <= 0 || section.LabHours <= 0)
                continue;

            // Skip sections that don't exceed threshold
            var total = section.TheoryHours + section.LabHours;
            if (total <= _splitThreshold)
                continue;

            var theoryInterval = ctx.Variables.GetLectureInterval(section.Id);
            var labInterval = ctx.Variables.GetLabInterval(section.Id);
            if (theoryInterval == null || labInterval == null)
                continue;

            // Get start IntVars from intervals (StartExpr returns LinearExpr; we need IntVar to use AddElement).
            // Approach: create helper IntVars equal to start expressions, then map via AddElement.
            var theoryStart = ctx.Model.NewIntVar(0, timeSlots.Count - 1, $"theory_start_idx_s{section.Id}");
            ctx.Model.Add(theoryStart == theoryInterval.StartExpr());

            var labStart = ctx.Model.NewIntVar(0, timeSlots.Count - 1, $"lab_start_idx_s{section.Id}");
            ctx.Model.Add(labStart == labInterval.StartExpr());

            // Day variables (DayOfWeek enum: 0=Sunday..6=Saturday)
            var theoryDay = ctx.Model.NewIntVar(0, 6, $"theory_day_s{section.Id}");
            var labDay = ctx.Model.NewIntVar(0, 6, $"lab_day_s{section.Id}");

            // AddElement: dayPerSlot[theoryStart] == theoryDay
            ctx.Model.AddElement(theoryStart, dayPerSlot, theoryDay);
            ctx.Model.AddElement(labStart, dayPerSlot, labDay);

            // Constraint: theoryDay != labDay (gated by assumption literal)
            var literal = ctx.Assumptions.Register(Code, primarySectionId: section.Id, secondarySectionId: null);
            ctx.Model.Add(theoryDay != labDay).OnlyEnforceIf(literal);
        }
    }
}
