using SIS.Domain.Enums;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    // ============================================================
    // Realistic grade distribution model (seed-only)
    // ============================================================
    // Replaces the old 3-band fixed grade patterns with a stochastic,
    // ability-conditioned ordered-cutpoint model. Produces a natural
    // bell-curve CGPA across the population plus realistic per-grade
    // frequencies, while every business rule (prereq / retake / retention)
    // still operates on the generated grades.
    //
    // Parameters anchored to published Turkish 4.0-scale university stats
    // (no live dataset import — see conversation): population CGPA mean
    // ~2.6, std ~0.55, retention-risk (<1.70) ~5-7%, ~8% in the 3.50+ band.
    //
    // Model:
    //   ability  θ ~ Normal(0, 1)                  (latent student ability)
    //   cgpa      = MeanCgpa + StdCgpa·θ            (linear, clamped)
    //   latentₖ   = θ + Normal(0, CourseSigma)      (per-course draw)
    //   gradeₖ    = first cutpoint with latentₖ ≥ threshold
    //
    // The cutpoints are calibrated so E[grade point | θ=0] ≈ 2.6 and the
    // population grade-point std lands near 0.55. Covered by
    // GradeDistributionTests (5000-draw mean/std/tail assertions).

    /// <summary>Population CGPA mean used to map latent ability ↔ CGPA.</summary>
    internal const decimal CgpaMean = 2.60m;

    /// <summary>Population CGPA standard deviation (1 ability unit = this many CGPA points).</summary>
    internal const decimal CgpaStd = 0.55m;

    /// <summary>Per-course noise added on top of student ability (in ability units).</summary>
    internal const double CourseSigma = 0.65;

    /// <summary>
    /// Scales how strongly ability shifts the latent draw. Calibrated so the expected
    /// grade-point response to ability has slope ≈ <see cref="CgpaStd"/>, which keeps
    /// the recomputed population CGPA std near 0.55 (the raw cutpoint slope is ~0.88).
    /// </summary>
    internal const double AbilityToLatentScale = 0.62;

    internal const decimal MinCgpa = 0.50m;
    internal const decimal MaxCgpa = 4.00m;

    /// <summary>
    /// Descending ordered cutpoints on the latent score. A draw is awarded the
    /// first grade whose threshold it clears; anything below the last threshold
    /// becomes NA (non-attendance, 0.00). Calibrated against CourseSigma.
    /// </summary>
    private static readonly (double Threshold, GradeCode Grade)[] GradeCutpoints =
    [
        (1.25, GradeCode.AA),
        (0.70, GradeCode.BA),
        (0.20, GradeCode.BB),
        (-0.30, GradeCode.CB),
        (-0.85, GradeCode.CC),
        (-1.40, GradeCode.DC),
        (-1.95, GradeCode.DD),
        (-2.50, GradeCode.FF),
        // below -2.50 → NA
    ];

    /// <summary>Maps a latent ability score to a provisional CGPA, clamped to a valid range.</summary>
    internal static decimal AbilityToCgpa(double ability)
    {
        var cgpa = CgpaMean + CgpaStd * (decimal)ability;
        return Math.Round(Math.Clamp(cgpa, MinCgpa, MaxCgpa), 2);
    }

    /// <summary>Recovers the latent ability that a given CGPA corresponds to (inverse of <see cref="AbilityToCgpa"/>).</summary>
    internal static double CgpaToAbility(decimal cgpa) => (double)((cgpa - CgpaMean) / CgpaStd);

    /// <summary>
    /// Draws a single GPA-bearing course grade for a student of the given ability.
    /// Higher ability shifts the latent draw upward, favouring higher grades.
    /// </summary>
    internal static GradeCode DrawGrade(double ability, Random rng)
    {
        var latent = ability * AbilityToLatentScale + NextNormal(rng) * CourseSigma;
        foreach (var (threshold, grade) in GradeCutpoints)
        {
            if (latent >= threshold)
                return grade;
        }
        return GradeCode.NA;
    }

    /// <summary>Standard normal sample via the Box–Muller transform (deterministic given a seeded <paramref name="rng"/>).</summary>
    internal static double NextNormal(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble(); // in (0, 1], avoids log(0)
        var u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
