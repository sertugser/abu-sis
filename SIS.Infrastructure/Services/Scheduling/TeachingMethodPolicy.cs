using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling;

/// <summary>
/// Tek otorite TeachingMethod davranış kuralları.
///
/// PROBLEM: TeachingMethod kontrolleri 5+ yere dağılmıştı (FacultyPlanningService,
/// AutoScheduleEngine, FaceToFaceSlotRestrictionEvaluator, CandidateGenerator,
/// SchedulingService). Her biri kendi mantığıyla "FaceToFace mı, Online mı, Hybrid mi"
/// sorusuna cevap veriyor — biri değiştiğinde diğerleri tutarsız kalıyordu.
///
/// Bu hafta yaşanan bug'lar:
///   - H17 evaluator FaceToFace kontrol ediyor, AssignSection FaceToFace kontrol ediyor,
///     CandidateGenerator FilterOutOnlineOnlySlots → 3 yer aynı kuralı tekrar yazdı.
///   - Online dersler için classroomId zorunluluğu 4 farklı yerde kontrol ediliyor.
///
/// ÇÖZÜM: tüm TeachingMethod-bazlı kararlar bu helper'dan geçer.
/// </summary>
public static class TeachingMethodPolicy
{
    /// <summary>
    /// Bu öğretim yöntemi derslik ataması GEREKTIRIR mi?
    /// Online dersler classroom-suz kabul edilir; FaceToFace ve Hybrid için zorunludur.
    /// </summary>
    public static bool RequiresClassroom(TeachingMethod method)
        => method != TeachingMethod.Online;

    /// <summary>
    /// Bu öğretim yöntemi OnlineOnly damgalı slot'lara YERLEŞEBİLİR mi?
    /// FaceToFace ders OnlineOnly slotta olamaz (H17 koruması).
    /// Online ve Hybrid dersler her slot tipine girebilir.
    /// </summary>
    public static bool CanUseOnlineOnlySlots(TeachingMethod method)
        => method != TeachingMethod.FaceToFace;

    /// <summary>
    /// Bu öğretim yöntemi FaceToFaceOnly damgalı slot'lara YERLEŞEBİLİR mi?
    /// Online ders FaceToFaceOnly slotta olmamalı (öğrenciler online katılım bekler).
    /// FaceToFace ve Hybrid dersler her slot tipine girebilir.
    /// </summary>
    public static bool CanUseFaceToFaceOnlySlots(TeachingMethod method)
        => method != TeachingMethod.Online;

    /// <summary>
    /// Bu yöntem hafta sonu / akşam akış kapsamına alınmış mıdır?
    /// (Default: Online dersler hafta sonu da olabilir.)
    /// </summary>
    public static bool AllowsWeekendSlots(TeachingMethod method)
        => method != TeachingMethod.FaceToFace;

    /// <summary>
    /// Bir ders için verilen slot tipi VE öğretim yöntemi uyumlu mu?
    /// H17 evaluator + manuel atama validation tek otorite kullanır.
    /// </summary>
    public static bool IsSlotModeCompatible(TeachingMethod method, TimeSlotAllowedMode slotMode)
        => slotMode switch
        {
            TimeSlotAllowedMode.Both => true,
            TimeSlotAllowedMode.OnlineOnly => CanUseOnlineOnlySlots(method),
            TimeSlotAllowedMode.FaceToFaceOnly => CanUseFaceToFaceOnlySlots(method),
            _ => true,
        };
}
