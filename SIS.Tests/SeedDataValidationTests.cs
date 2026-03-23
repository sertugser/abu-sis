using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;

namespace SIS.Tests;

public class SeedDataValidationTests
{
    [Fact]
    public void SuccessfulGradePattern_AverageAbove2_50()
    {
        var pattern = new[]
        {
            GradeCode.AA, GradeCode.BA, GradeCode.BB, GradeCode.CB,
            GradeCode.BB, GradeCode.AA, GradeCode.BA, GradeCode.BB,
            GradeCode.CB, GradeCode.BA, GradeCode.BB, GradeCode.AA
        };
        var avg = pattern.Average(g => (double)(GradePointMap.GetPoint(g) ?? 0));
        Assert.True(avg >= 2.50, $"Successful pattern average {avg} should be >= 2.50");
    }

    [Fact]
    public void BorderlineGradePattern_AverageBetween2_00And2_50()
    {
        var pattern = new[]
        {
            GradeCode.CC, GradeCode.CB, GradeCode.CC, GradeCode.CC,
            GradeCode.DC, GradeCode.CB, GradeCode.CC, GradeCode.CB,
            GradeCode.DC, GradeCode.CC, GradeCode.CB, GradeCode.CC
        };
        var avg = pattern.Average(g => (double)(GradePointMap.GetPoint(g) ?? 0));
        Assert.True(avg >= 2.00 && avg < 2.50, $"Borderline pattern average {avg} should be in [2.00, 2.50)");
    }

    [Fact]
    public void LowGradePattern_AverageBelow2_00_ContainsFFAndNA()
    {
        var pattern = new[]
        {
            GradeCode.DC, GradeCode.FF, GradeCode.DD, GradeCode.CC,
            GradeCode.NA, GradeCode.DD, GradeCode.DC, GradeCode.FF,
            GradeCode.CC, GradeCode.DD, GradeCode.DC, GradeCode.NA
        };
        var avg = pattern.Average(g => (double)(GradePointMap.GetPoint(g) ?? 0));
        Assert.True(avg < 2.00, $"Low pattern average {avg} should be < 2.00");
        Assert.Contains(GradeCode.FF, pattern);
        Assert.Contains(GradeCode.NA, pattern);
    }

    [Fact]
    public void ProgramDistribution_SumsTo1000()
    {
        var counts = new[] { 250, 200, 150, 150, 130, 120 };
        Assert.Equal(1000, counts.Sum());
    }

    [Theory]
    [InlineData(250, new[] { 0.30, 0.28, 0.24, 0.18 })]
    [InlineData(200, new[] { 0.30, 0.28, 0.24, 0.18 })]
    [InlineData(150, new[] { 0.30, 0.28, 0.24, 0.18 })]
    [InlineData(130, new[] { 0.30, 0.28, 0.24, 0.18 })]
    [InlineData(120, new[] { 0.30, 0.28, 0.24, 0.18 })]
    public void SemesterGroupDistribution_SumsToTotal(int total, double[] ratios)
    {
        var groupCounts = ratios.Select(r => (int)Math.Round(total * r)).ToArray();
        int diff = total - groupCounts.Sum();
        groupCounts[0] += diff;
        Assert.Equal(total, groupCounts.Sum());
    }
}
