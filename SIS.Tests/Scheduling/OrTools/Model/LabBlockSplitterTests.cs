using FluentAssertions;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Model;

public class LabBlockSplitterTests
{
    [Theory]
    [InlineData(0, 2, new int[0])]
    [InlineData(1, 2, new[] { 1 })]
    [InlineData(2, 2, new[] { 2 })]
    [InlineData(3, 2, new[] { 2, 1 })]
    [InlineData(4, 2, new[] { 2, 2 })]
    [InlineData(5, 2, new[] { 2, 2, 1 })]
    [InlineData(6, 2, new[] { 2, 2, 2 })]
    [InlineData(7, 2, new[] { 2, 2, 2, 1 })]
    public void SplitLabIntoBlocks_MaxBlockTwoHours_MatchesV1Algorithm(
        int totalLabHours, int maxBlockHours, int[] expected)
    {
        var result = LabBlockSplitter.SplitLabIntoBlocks(totalLabHours, maxBlockHours);

        result.Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
    }

    [Theory]
    [InlineData(5, 3, new[] { 3, 2 })]
    [InlineData(6, 3, new[] { 3, 3 })]
    [InlineData(7, 3, new[] { 3, 3, 1 })]
    [InlineData(8, 4, new[] { 4, 4 })]
    public void SplitLabIntoBlocks_LargerMaxBlock_BreaksIntoFewer(
        int totalLabHours, int maxBlockHours, int[] expected)
    {
        var result = LabBlockSplitter.SplitLabIntoBlocks(totalLabHours, maxBlockHours);

        result.Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
    }

    [Fact]
    public void SplitLabIntoBlocks_NegativeTotal_ReturnsEmpty()
    {
        var result = LabBlockSplitter.SplitLabIntoBlocks(-3, 2);

        result.Should().BeEmpty();
    }

    [Fact]
    public void SplitLabIntoBlocks_ZeroMaxBlock_DefensiveReturnsEmpty()
    {
        // maxBlockHours <= 0 must not loop forever; defensive guard.
        var result = LabBlockSplitter.SplitLabIntoBlocks(4, 0);

        result.Should().BeEmpty();
    }

    [Fact]
    public void SplitLabIntoBlocks_BlocksSumEqualsTotal_Always()
    {
        for (int total = 1; total <= 10; total++)
        {
            for (int max = 1; max <= 5; max++)
            {
                var blocks = LabBlockSplitter.SplitLabIntoBlocks(total, max);
                blocks.Sum().Should().Be(total,
                    because: $"sum invariant for total={total}, max={max}");
            }
        }
    }
}
