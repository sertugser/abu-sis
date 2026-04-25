using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

/// <summary>
/// Sabit zaman grid'i slotu. Dönem başında SystemConfig parametrelerinden generate edilir.
/// Her slot bir günün belirli saatindeki 50 dakikalık (veya config'e göre) ders periyodunu temsil eder.
/// Dönem boyunca sabit kalır, config değişikliği sonraki dönem için geçerli olur.
/// </summary>
public class TimeSlot
{
    public int Id { get; set; }
    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int SlotOrder { get; set; }  // Günün kaçıncı slotu (1, 2, 3...)
    public string DisplayName { get; set; } = string.Empty;  // e.g., "Pazartesi 1. Ders (09:00-09:50)"

    /// <summary>
    /// Bu slotta hangi tür dersin yer alabileceğini belirler.
    /// Both: her türlü ders; OnlineOnly: yalnızca online/hybrid; FaceToFaceOnly: yalnızca yüz-yüze/hybrid.
    /// Generate sırasında 18:00+ ve hafta sonu slotlar otomatik OnlineOnly damgalanır, admin sonradan değiştirebilir.
    /// </summary>
    public TimeSlotAllowedMode AllowedMode { get; set; } = TimeSlotAllowedMode.Both;
}
