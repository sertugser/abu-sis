namespace SIS.Domain.Entities;

public class CurriculumElectivePool
{
    public int Id { get; set; }
    public int CurriculumId { get; set; }
    public Curriculum Curriculum { get; set; } = null!;
    public int ElectiveGroupId { get; set; }
    public ElectiveGroup ElectiveGroup { get; set; } = null!;

    /// <summary>
    /// Aynı havuzun aynı müfredata farklı slotlar olarak bağlanmasını sağlar.
    /// Örn: "TE-1", "TE-2", "OC-1". (CurriculumId, ElectiveGroupId, SlotName) unique.
    /// </summary>
    public string SlotName { get; set; } = string.Empty;

    public int SuggestedSemesterStart { get; set; }
    public int SuggestedSemesterEnd { get; set; }
    public int RequiredCourseCount { get; set; }
    public int RequiredEcts { get; set; }

    /// <summary>
    /// Bu havuzdaki her dersin müfredatta kaç AKTS olarak sayılacağı (sabit değer).
    /// Null ise dersin katalog AKTS'si kullanılır (geçiş dönemi için geriye uyumlu).
    /// Örn: NDE=3, TE=5, OC=0.
    /// </summary>
    public int? EctsPerCourse { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
