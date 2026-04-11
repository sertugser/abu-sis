namespace SIS.Application.Scheduling.Services;

public interface ITimeSlotService
{
    /// <summary>
    /// SystemConfig parametrelerinden dönem için TimeSlot grid'i oluşturur.
    /// Dönem başında (Draft → Planning geçişinde) çağrılır.
    /// Zaten slot varsa tekrar oluşturmaz.
    /// </summary>
    Task<int> GenerateTimeSlotsForTermAsync(int termId, CancellationToken ct = default);
}
