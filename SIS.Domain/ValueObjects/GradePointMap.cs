using SIS.Domain.Enums;

namespace SIS.Domain.ValueObjects;

public static class GradePointMap
{
    public static decimal? GetPoint(GradeCode grade) => grade switch
    {
        GradeCode.AA => 4.00m,
        GradeCode.BA => 3.50m,
        GradeCode.BB => 3.00m,
        GradeCode.CB => 2.50m,
        GradeCode.CC => 2.00m,
        GradeCode.DC => 1.50m,
        GradeCode.DD => 1.00m,
        GradeCode.FF => 0.00m,
        GradeCode.NA => 0.00m,
        _             => null,
    };

    public static bool IsGpaIncluded(GradeCode grade) => GetPoint(grade) != null;
}
