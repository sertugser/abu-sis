using FluentAssertions;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Tests.DomainTests;

public class GradingCriteriaTests
{
    private readonly GradingCriteria _defaultCriteria = new();

    // Varsayılan aralıklar: AA≥91, BA≥81, BB≥71, CB≥61, CC≥55, DC≥50, DD≥45, FF<45

    [Theory]
    [InlineData(100, GradeCode.AA)]
    [InlineData(91, GradeCode.AA)]
    [InlineData(90, GradeCode.BA)]
    [InlineData(81, GradeCode.BA)]
    [InlineData(80, GradeCode.BB)]
    [InlineData(71, GradeCode.BB)]
    [InlineData(70, GradeCode.CB)]
    [InlineData(61, GradeCode.CB)]
    [InlineData(60, GradeCode.CC)]
    [InlineData(55, GradeCode.CC)]
    [InlineData(54, GradeCode.DC)]
    [InlineData(50, GradeCode.DC)]
    [InlineData(49, GradeCode.DD)]
    [InlineData(45, GradeCode.DD)]
    [InlineData(44, GradeCode.FF)]
    [InlineData(0, GradeCode.FF)]
    public void ResolveGrade_DefaultRanges_ReturnsCorrectLetterGrade(int score, GradeCode expected)
    {
        _defaultCriteria.ResolveGrade(score).Should().Be(expected);
    }

    [Fact]
    public void ResolveGrade_CurvedRanges_UsesInstructorDefinedRanges()
    {
        // Hoca çan uygulamış — aralıkları düşürmüş
        var curved = new GradingCriteria
        {
            AAMin = 85,
            BAMin = 75,
            BBMin = 65,
            CBMin = 55,
            CCMin = 50,
            DCMin = 45,
            DDMin = 40
        };

        // Varsayılanda 76 → BB, çan'da 76 → BA (BAMin=75)
        curved.ResolveGrade(76).Should().Be(GradeCode.BA);

        // Varsayılanda 44 → FF, çan'da 44 → DD
        curved.ResolveGrade(44).Should().Be(GradeCode.DD);

        // Varsayılanda 39 → FF, çan'da 39 → FF (hâlâ FF)
        curved.ResolveGrade(39).Should().Be(GradeCode.FF);
    }

    [Fact]
    public void ResolveGrade_BoundaryValues_CorrectlyClassified()
    {
        // Tam sınır değerlerinde doğru çalıştığını doğrula
        _defaultCriteria.ResolveGrade(91).Should().Be(GradeCode.AA);   // AA alt sınırı
        _defaultCriteria.ResolveGrade(90).Should().Be(GradeCode.BA);   // AA'nın 1 altı = BA
        _defaultCriteria.ResolveGrade(45).Should().Be(GradeCode.DD);   // DD alt sınırı
        _defaultCriteria.ResolveGrade(44).Should().Be(GradeCode.FF);   // DD'nin 1 altı = FF
    }
}
