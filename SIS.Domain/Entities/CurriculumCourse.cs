namespace SIS.Domain.Entities;

public class CurriculumCourse
{
    public int CurriculumId { get; set; }
    public Curriculum Curriculum { get; set; } = null!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public bool IsCompulsory { get; set; }
    public int SuggestedSemester { get; set; }             // which semester this course is typically taken
    public bool IncludeInGpa { get; set; } = true;          // Ortalamalara Katılım
    public bool ShowOnTranscript { get; set; } = true;       // Transkriptte Göster
    public string? CourseCategory { get; set; }               // Ders Kategorisi (Dönemelik, Yıllık, vb.)
    public string? OpeningReason { get; set; }                // Açma Nedeni
    public string? GroupCode { get; set; }                    // Grup Kodu (seçmeli grup)
    public int? ElectiveGroupId { get; set; }
    public ElectiveGroup? ElectiveGroup { get; set; }
}
