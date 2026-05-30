namespace SIS.Application.Scheduling.DTOs;

public sealed record StudentTimetableDto(
    string StudentName,
    int TermId,
    string TermName,
    List<StudentTimetableSectionDto> Sections,
    // Sistem ayarından (Schedule.LunchBreakStart/End) gelen öğle arası penceresi, "HH:mm".
    string LunchBreakStart,
    string LunchBreakEnd,
    // Term'in tanımlı ders saati pencereleri — takvim ızgarasının satır yapısını besler.
    List<TimetableSlotDto> Slots);
