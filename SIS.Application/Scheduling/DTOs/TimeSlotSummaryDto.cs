namespace SIS.Application.Scheduling.DTOs;

/// <summary>
/// Bir dönemin ders saat slotunun (SlotOrder bazında) özet gösterimi.
/// Zaman ekseninin tek kaynağı: derslik doluluk ekranındaki tüm saat odaklı
/// görünümler (Boş Ara formu, haftalık ızgara) bu listeden beslenir.
/// </summary>
public class TimeSlotSummaryDto
{
    public int SlotOrder { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
