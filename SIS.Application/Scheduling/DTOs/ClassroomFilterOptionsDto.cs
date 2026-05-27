namespace SIS.Application.Scheduling.DTOs;

public class ClassroomFilterOptionsDto
{
    public List<ClassroomFacultyOption> Faculties { get; set; } = new();
    public List<string> Buildings { get; set; } = new();
    public List<string> ClassroomTypes { get; set; } = new();

    /// <summary>
    /// Dönemin gerçek ders saat slotları (SlotOrder bazında tekil). Frontend "Boş Ara"
    /// formundaki saat seçimini ve haftalık ızgaranın satırlarını besler — sabit 08-17
    /// yerine dönemin akşam slotları dahil gerçek aralığı kullanılır.
    /// </summary>
    public List<TimeSlotSummaryDto> Slots { get; set; } = new();
}

public class ClassroomFacultyOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
