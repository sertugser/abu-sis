using FluentAssertions;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.InfrastructureTests;

/// <summary>
/// Calibration tests for the seed grade-distribution model. They simulate a
/// population of students (ability θ ~ N(0,1)), draw a full transcript of
/// grades per student, recompute each CGPA, and assert the population shape
/// matches the published Turkish 4.0-scale targets the model is anchored to.
/// Pure (no DB) — fast enough to assert distribution statistics directly.
/// </summary>
public class GradeDistributionTests
{
    private const int StudentCount = 5000;
    private const int CoursesPerStudent = 40;

    private static List<decimal> SimulatePopulationCgpas(int seed)
    {
        var rng = new Random(seed);
        var cgpas = new List<decimal>(StudentCount);

        for (var s = 0; s < StudentCount; s++)
        {
            var ability = SeedData.NextNormal(rng);
            decimal qualityPoints = 0;
            var courses = 0;

            for (var c = 0; c < CoursesPerStudent; c++)
            {
                var grade = SeedData.DrawGrade(ability, rng);
                var point = GradePointMap.GetPoint(grade) ?? 0m;
                qualityPoints += point;
                courses++;
            }

            cgpas.Add(Math.Round(qualityPoints / courses, 2));
        }

        return cgpas;
    }

    [Fact]
    public void Population_CgpaMean_IsNearTarget()
    {
        var cgpas = SimulatePopulationCgpas(seed: 42);

        var mean = cgpas.Average();

        mean.Should().BeInRange(2.45m, 2.75m, "population CGPA mean is anchored to ~2.60");
    }

    [Fact]
    public void Population_CgpaStdDev_IsNearTarget()
    {
        var cgpas = SimulatePopulationCgpas(seed: 42);
        var mean = cgpas.Average();

        var variance = cgpas.Sum(c => (double)((c - mean) * (c - mean))) / cgpas.Count;
        var std = Math.Sqrt(variance);

        std.Should().BeInRange(0.40, 0.70, "population CGPA std is anchored to ~0.55");
    }

    [Fact]
    public void Population_RetentionRiskFraction_IsRealistic()
    {
        var cgpas = SimulatePopulationCgpas(seed: 42);

        var belowThreshold = cgpas.Count(c => c < 1.70m) / (double)cgpas.Count;

        belowThreshold.Should().BeInRange(0.03, 0.12, "~5-7% of students should sit under the 1.70 retention threshold");
    }

    [Fact]
    public void Population_HighAchieverFraction_IsRealistic()
    {
        var cgpas = SimulatePopulationCgpas(seed: 42);

        var highAchievers = cgpas.Count(c => c >= 3.50m) / (double)cgpas.Count;

        highAchievers.Should().BeInRange(0.03, 0.15, "a meaningful but small minority should reach the 3.50+ band");
    }

    [Fact]
    public void DrawGrade_IsDeterministic_ForSameSeed()
    {
        var first = DrawSequence(seed: 7);
        var second = DrawSequence(seed: 7);

        second.Should().Equal(first, "a fixed seed must reproduce the same transcript");

        static List<GradeCode> DrawSequence(int seed)
        {
            var rng = new Random(seed);
            return Enumerable.Range(0, 50).Select(_ => SeedData.DrawGrade(0.0, rng)).ToList();
        }
    }

    [Fact]
    public void DrawGrade_HigherAbility_YieldsHigherAverageGrade()
    {
        var rngLow = new Random(99);
        var rngHigh = new Random(99);

        var lowAvg = AverageGradePoint(ability: -1.0, rngLow);
        var highAvg = AverageGradePoint(ability: 1.0, rngHigh);

        highAvg.Should().BeGreaterThan(lowAvg, "ability must monotonically shift grades upward");

        static decimal AverageGradePoint(double ability, Random rng)
        {
            decimal sum = 0;
            const int n = 2000;
            for (var i = 0; i < n; i++)
                sum += GradePointMap.GetPoint(SeedData.DrawGrade(ability, rng)) ?? 0m;
            return sum / n;
        }
    }

    [Fact]
    public void AbilityToCgpa_And_CgpaToAbility_RoundTrip()
    {
        foreach (var cgpa in new[] { 1.20m, 2.00m, 2.60m, 3.10m, 3.80m })
        {
            var ability = SeedData.CgpaToAbility(cgpa);
            var roundTrip = SeedData.AbilityToCgpa(ability);

            roundTrip.Should().BeApproximately(cgpa, 0.01m, "the ability ↔ CGPA map must be a clean inverse within the valid range");
        }
    }
}
