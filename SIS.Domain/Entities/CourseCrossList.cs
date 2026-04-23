namespace SIS.Domain.Entities;

/// <summary>
/// İki ders kodunun aynı dersi temsil ettiğini gösteren cross-listing kaydı.
/// Örn: YZM301 ve BIL301 aynı dersin farklı bölümlerdeki görünümüdür.
/// N:N ilişki — symmetric olarak iki kayıt tutulur ({YZM301,BIL301} ve {BIL301,YZM301}).
/// </summary>
public class CourseCrossList
{
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public int LinkedCourseId { get; set; }
    public Course LinkedCourse { get; set; } = null!;

    /// <summary>
    /// Bu eşlemede asıl (controlling) ders mi? Primary olan şube üretir,
    /// diğerleri onun kayıt listesini paylaşır.
    /// </summary>
    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; }
}
