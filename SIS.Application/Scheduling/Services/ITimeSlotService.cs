using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.Scheduling.Services;

public interface ITimeSlotService
{
    /// <summary>
    /// SystemConfig parametrelerinden dönem için TimeSlot grid'i oluşturur.
    /// Dönem başında (Draft → Planning geçişinde) çağrılır.
    /// Zaten slot varsa tekrar oluşturmaz.
    /// </summary>
    Task<int> GenerateTimeSlotsForTermAsync(int termId, CancellationToken ct = default);

    Task<IReadOnlyList<TimeSlotDto>> GetByTermAsync(int termId, CancellationToken ct = default);

    Task<TimeSlotDto> CreateAsync(CreateTimeSlotDto dto, CancellationToken ct = default);

    Task<TimeSlotDto> UpdateAsync(int id, UpdateTimeSlotDto dto, CancellationToken ct = default);

    /// <summary>
    /// Slotu siler. force=false ve bağımlı SectionSchedule varsa silmez, sayıyı döner (UI uyarı diyaloğu için).
    /// force=true ise bağımlı SectionSchedule'lar cascade silinir.
    /// </summary>
    Task<TimeSlotDeleteResultDto> DeleteAsync(int id, bool force, CancellationToken ct = default);

    /// <summary>
    /// Dönemin tüm slotlarını siler ve config'den yeniden üretir. Bağımlı SectionSchedule varsa regenerate etmez,
    /// force=true ile cascade silme opsiyonu sunulur.
    /// </summary>
    Task<RegenerateTimeSlotsResultDto> RegenerateForTermAsync(int termId, bool force, CancellationToken ct = default);
}
