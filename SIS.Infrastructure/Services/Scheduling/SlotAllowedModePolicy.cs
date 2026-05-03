using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling;

/// <summary>
/// Tek otorite slot AllowedMode kontrolleri.
///
/// PROBLEM: AllowedMode kontrolleri 4 yere dağılmıştı:
///   - CandidateGenerator (filtre)
///   - FacultyPlanningService.AssignSection (validate, line 367-407)
///   - FaceToFaceSlotRestrictionEvaluator (H17 evaluator)
///   - AutoScheduleEngine.InvalidateAssignmentsViolatingSlotMode (retroactive)
///
/// Her biri kendi mantığıyla check yapıyordu, küçük farklar bug'a yol açıyor.
///
/// ÇÖZÜM: tüm slot-mode kontrolleri bu helper üzerinden TeachingMethodPolicy'i çağırır.
/// </summary>
public static class SlotAllowedModePolicy
{
    /// <summary>
    /// Bir TimeSlot belirtilen ders için kullanılabilir mi?
    /// </summary>
    public static bool IsAllowedForCourse(TimeSlotAllowedMode slotMode, TeachingMethod method)
        => TeachingMethodPolicy.IsSlotModeCompatible(method, slotMode);

    /// <summary>
    /// Slot listesinden ders için kullanılamayacak slotları çıkarır.
    /// CandidateGenerator filtreleme için.
    /// </summary>
    public static IEnumerable<TimeSlot> FilterIncompatibleSlots(
        IEnumerable<TimeSlot> slots,
        TeachingMethod method)
        => slots.Where(s => IsAllowedForCourse(s.AllowedMode, method));

    /// <summary>
    /// Verilen (Day, StartTime) slotunu mode-haritasından çek.
    /// Lookup yoksa varsayılan Both döner (en serbest).
    /// </summary>
    public static TimeSlotAllowedMode GetMode(
        IReadOnlyDictionary<(DayOfWeek Day, TimeOnly StartTime), TimeSlotAllowedMode> slotModes,
        DayOfWeek day,
        TimeOnly startTime)
    {
        return slotModes.TryGetValue((day, startTime), out var mode) ? mode : TimeSlotAllowedMode.Both;
    }

    /// <summary>
    /// Verilen TimeBlock'ların biri/birkaçı belirtilen ders için yasak mı?
    /// FacultyPlanningService.AssignSection'da retroaktif kontrol için.
    /// </summary>
    public static List<(DayOfWeek Day, TimeOnly StartTime)> FindViolatingSlots(
        IEnumerable<(DayOfWeek Day, TimeOnly StartTime)> requestedSlots,
        IReadOnlyDictionary<(DayOfWeek Day, TimeOnly StartTime), TimeSlotAllowedMode> slotModes,
        TeachingMethod method)
    {
        return requestedSlots
            .Where(s => !IsAllowedForCourse(GetMode(slotModes, s.Day, s.StartTime), method))
            .ToList();
    }
}
