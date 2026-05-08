using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// DIST — Distribution Constraints (Plan 6 Task 10 stub).
/// 6 distribution type (SameDay/DifferentDay/SameRoom/BackToBack/NotBackToBack/Precedence).
/// 2 tier (Hard=Unavailable, Soft=Discouraged/StronglyDiscouraged).
///
/// Plan 6 Task 10: infrastructure-only. SolverRequest.DistributionConstraints loaded from DB
/// but encoder body diagnostic-only — real CP-SAT enforcement deferred to Plan 6.5
/// (DB has 0 rows currently; multi-type/multi-tier logic non-trivial).
/// 1 assumption per constraint instance for future UnsatCore attribution.
/// </summary>
public sealed class DistributionConstraintEncoder : IConstraintEncoder
{
    public string Code => "DIST";
    public bool IsHard => true; // Unavailable tier; soft tier (Discouraged) Plan 8 (penalties)

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;

        // Plan 6 Task 10 stub: register 1 assumption per constraint, no actual constraints added.
        // Plan 6.5 will read DistributionType + PreferenceLevel and add real CP-SAT constraints.
        foreach (var dist in ctx.Request.DistributionConstraints)
        {
            ctx.Assumptions.Register(Code,
                primarySectionId: dist.Id,
                secondarySectionId: null);
        }
    }
}
