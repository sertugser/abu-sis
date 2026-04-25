using SIS.Domain.Enums;

namespace SIS.Application.Scheduling.DTOs;

public sealed record TimeSlotDto(
    int Id,
    int TermId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int SlotOrder,
    string DisplayName,
    TimeSlotAllowedMode AllowedMode);

public sealed record CreateTimeSlotDto(
    int TermId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeSlotAllowedMode AllowedMode);

public sealed record UpdateTimeSlotDto(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeSlotAllowedMode AllowedMode);

/// <summary>
/// TimeSlot silme denemesinin sonucu. Silinmeden önce bağımlı SectionSchedule sayısı raporlanır.
/// UI bu bilgiyi AlertDialog'da gösterip kullanıcıdan force=true ile onay alır.
/// </summary>
public sealed record TimeSlotDeleteResultDto(
    int Id,
    int DependentScheduleCount,
    bool Deleted,
    string? Message);

/// <summary>
/// Dönem için tüm slotları silip config'den yeniden üretir. Bağımlı SectionSchedule varsa hata.
/// </summary>
public sealed record RegenerateTimeSlotsResultDto(
    int TermId,
    int GeneratedCount,
    int DependentScheduleCount,
    bool Regenerated,
    string? Message);
