namespace SIS.Domain.Enums;

/// <summary>
/// Müfredat dönem kural tipleri (Proliz "Müfredat Dönem Kuralları" muadili).
/// İlk 14 kural; yeni kural eklenirken enum sonuna eklenmeli — DB'de tam sayı tutulur.
/// </summary>
public enum CurriculumTermRuleType
{
    /// <summary>Alması Gereken En Az Kredi</summary>
    MinRequiredCredit = 1,

    /// <summary>Alması Gereken En Az Ders Sayısı</summary>
    MinRequiredCourseCount = 2,

    /// <summary>Alması Gereken En Az Ders Saati</summary>
    MinRequiredCourseHours = 3,

    /// <summary>Alabileceği En Fazla Kredi</summary>
    MaxAllowedCredit = 4,

    /// <summary>Alabileceği En Fazla Ders Saati</summary>
    MaxAllowedCourseHours = 5,

    /// <summary>Alabileceği En Fazla AKTS</summary>
    MaxAllowedEcts = 6,

    /// <summary>Üstten Ders Alabilmesi İçin Gerekli En Az Ortalama</summary>
    MinGpaForUpperCourse = 7,

    /// <summary>Üstten Alabileceği En Fazla Kredi</summary>
    MaxUpperAllowedCredit = 8,

    /// <summary>Üstten Alabileceği En Fazla Ders Sayısı</summary>
    MaxUpperAllowedCourseCount = 9,

    /// <summary>Üstten Alabileceği En Fazla Ders Saati</summary>
    MaxUpperAllowedCourseHours = 10,

    /// <summary>Üstten Alabileceği En Fazla AKTS</summary>
    MaxUpperAllowedEcts = 11,

    /// <summary>Not Yük. Amacıyla Ders Alabilmesi İçin En Az Ort.</summary>
    MinGpaForGradeImprovement = 12,

    /// <summary>Not Yükseltme için Alabileceği En Fazla Kredi</summary>
    MaxGradeImprovementCredit = 13,

    /// <summary>Alması Gereken En Az AKTS</summary>
    MinRequiredEcts = 14,
}
