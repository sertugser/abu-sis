using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

/// <summary>
/// Sabit zaman grid'i. Bir kez tanımlanır, tüm dönemler aynı slotları kullanır.
/// Günler Pattern'dan türetilir (MWF=Pzt+Çar+Cum, TR=Sal+Per).
/// </summary>
public class TimeSlot
{
    public int Id { get; set; }
    public TimeSlotPattern Pattern { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public SlotType SlotType { get; set; }
    public string DisplayName { get; set; } = string.Empty;  // e.g., "MWF 09:00-09:50"
}
