namespace SIS.Application.Scheduling.DTOs;

/// <summary>
/// Term'in tanımlı bir ders saati penceresi ("HH:mm"). Takvim ızgarasının satır yapısı
/// (saat çizgileri + görünür aralık) bu tanımlı slotlardan üretilir — hardcoded saat yok.
/// </summary>
public sealed record TimetableSlotDto(string Start, string End);
