namespace SIS.Application.Common.DTOs;

/// <summary>
/// Bir dersin (Course) intibak sayfasındaki öğrenci istatistikleri.
/// "Dersi alan" = bu dersin herhangi bir şubesine gerçek bir kayıtla (Active/Completed/Withdrawn)
/// kayıtlanmış DISTINCT öğrenci. Başarılı/başarısız öğrencinin en iyi denemesine göre belirlenir
/// (geçer bir notu varsa başarılı; yoksa ve başarısız notu varsa başarısız — RegistrationRulePolicy).
/// Total/Passed/Failed TÜM dönemleri kapsar; Active* alanları yalnızca AKTİF (Kullanıma Açık) dönemde
/// bu dersi alan öğrencileri kapsar (aktif dönem yoksa 0).
/// </summary>
public sealed record CourseIntibakStatisticsDto(
    int CourseId,
    int TotalStudents,
    int PassedStudents,
    int FailedStudents,
    int ActiveStudents,
    int ActivePassedStudents,
    int ActiveFailedStudents);
