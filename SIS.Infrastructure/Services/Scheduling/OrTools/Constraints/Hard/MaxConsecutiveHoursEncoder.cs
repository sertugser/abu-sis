namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// H15 — Maximum Consecutive Hours per Instructor per Day.
/// Hoca aynı gün içinde aralıksız <see cref="_maxHoursPerDay"/> saatten fazla ders veremez.
///
/// PLAN 2 SCOPE: Diagnostic-only — registers per-instructor assumption literals so UNSAT core
/// can attribute infeasibilities to H15. Does NOT add actual cumulative/sliding-window
/// constraints to the CP-SAT model.
///
/// PLAN 3 SCOPE: Real cumulative implementation. Requires:
/// - Day-of-slot mapping (similar to H16 AddElement pattern)
/// - Per-(instructor, day) sliding window analysis
/// - Performance benchmark (sliding-window constraints can blow up model size)
///
/// Until Plan 3 lands, v1 legacy <c>MaxConsecutiveHoursEvaluator</c> continues to enforce this
/// constraint when v1 engine is selected. v2 engine produces schedules that may violate H15
/// — acceptable for Plan 2 PoC stage.
///
/// <c>_maxHoursPerDay</c> SystemConfig.Schedule.MaxConsecutiveHoursPerDay'tan okunur (default 4).
/// </summary>
public sealed class MaxConsecutiveHoursEncoder : IConstraintEncoder
{
    public string Code => "H15";
    public bool IsHard => true;

    private readonly int _maxHoursPerDay;

    public MaxConsecutiveHoursEncoder(int maxHoursPerDay = 4)
    {
        if (maxHoursPerDay <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHoursPerDay),
                maxHoursPerDay, "maxHoursPerDay must be positive.");
        _maxHoursPerDay = maxHoursPerDay;
    }

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true))
            return;

        // Plan 2 diagnostic-only: register one assumption literal per distinct instructor.
        // Plan 3 will add actual cumulative constraints here.
        var instructorIds = ctx.Request.Sections
            .Where(s => s.InstructorId.HasValue)
            .Select(s => s.InstructorId!.Value)
            .Distinct();

        foreach (var instructorId in instructorIds)
        {
            ctx.Assumptions.Register(Code,
                primarySectionId: instructorId,   // overload primary as instructor id for diagnostic
                secondarySectionId: null);
        }
    }
}
