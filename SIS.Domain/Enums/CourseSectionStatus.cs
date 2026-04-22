namespace SIS.Domain.Enums;

/// <summary>
/// CourseSection lifecycle durumu. Her section ayrı durum yönetir.
/// Draft → PendingApproval → Active [→ StopEnrollment] [→ Cancelled]
/// </summary>
public enum CourseSectionStatus
{
    /// <summary>Hazırlık: hoca/derslik/slot atanmamış</summary>
    Draft = 0,

    /// <summary>Atama tamam, fakülte dekan onayı bekliyor</summary>
    PendingApproval = 1,

    /// <summary>Onaylandı — öğrenciler kayıt olabilir</summary>
    Active = 2,

    /// <summary>Yeni kayıt alınmıyor (kapasite dolu vb.), mevcutlar geçerli</summary>
    StopEnrollment = 3,

    /// <summary>İptal edildi — tüm kayıtlar iptal, ders işlenmez</summary>
    Cancelled = 4
}
