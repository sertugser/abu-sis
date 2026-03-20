using FluentAssertions;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;

namespace SIS.Tests.DomainTests;

public class GradePointMapTests
{
    [Theory]
    [InlineData(GradeCode.AA, 4.00)]
    [InlineData(GradeCode.BA, 3.50)]
    [InlineData(GradeCode.BB, 3.00)]
    [InlineData(GradeCode.CB, 2.50)]
    [InlineData(GradeCode.CC, 2.00)]
    [InlineData(GradeCode.DC, 1.50)]
    [InlineData(GradeCode.DD, 1.00)]
    [InlineData(GradeCode.FF, 0.00)]
    [InlineData(GradeCode.NA, 0.00)]
    public void GetPoint_LetterGrade_ReturnsCorrectCoefficient(GradeCode grade, double expected)
    {
        var result = GradePointMap.GetPoint(grade);

        result.Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData(GradeCode.S)]
    [InlineData(GradeCode.U)]
    [InlineData(GradeCode.W)]
    [InlineData(GradeCode.I)]
    [InlineData(GradeCode.NI)]
    public void GetPoint_NonGpaGrade_ReturnsNull(GradeCode grade)
    {
        var result = GradePointMap.GetPoint(grade);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(GradeCode.AA, true)]
    [InlineData(GradeCode.FF, true)]
    [InlineData(GradeCode.NA, true)]
    [InlineData(GradeCode.S, false)]
    [InlineData(GradeCode.U, false)]
    [InlineData(GradeCode.W, false)]
    [InlineData(GradeCode.I, false)]
    [InlineData(GradeCode.NI, false)]
    public void IsGpaIncluded_ReturnsCorrectResult(GradeCode grade, bool expected)
    {
        GradePointMap.IsGpaIncluded(grade).Should().Be(expected);
    }
}
