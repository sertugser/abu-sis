namespace SIS.Domain.Entities;

using SIS.Domain.Enums;

public class ExamRegulation
{
    public int Id { get; set; }

    // Kapsam (unique: ProgramId + DegreeLevel + TermType + Year)
    public int ProgramId { get; set; }
    public AcademicProgram Program { get; set; } = null!;
    public DegreeLevel DegreeLevel { get; set; } = DegreeLevel.Bachelor; // Lisans/Önlisans
    public TermType TermType { get; set; } = TermType.Fall;
    public int Year { get; set; }

    public bool IsActive { get; set; } = true;
    public string Name { get; set; } = string.Empty;

    // Sınav yapısı
    public int MidtermCount { get; set; } = 1;   // Ara Sınav Sayısı
    public int FinalCount { get; set; } = 1;     // YYS Sınav Sayısı

    // Etki oranı sınırları (hoca ağırlık girişine Faz 2'de dayatılacak)
    public decimal MinMidtermWeightPercent { get; set; } = 40m;
    public decimal MaxMidtermWeightPercent { get; set; } = 60m;
    public decimal MinFinalWeightPercent { get; set; } = 40m;
    public decimal MaxFinalWeightPercent { get; set; } = 60m;

    // Geçme / baraj (Faz 2'de Finalize'a bağlanacak; bu faz sadece saklanır)
    public decimal PassingGrade { get; set; } = 0m;       // Geçme Notu
    public decimal RawScoreFloor1 { get; set; } = 0m;     // Ham Başarı Notu Alt Limiti
    public decimal RawScoreFloor2 { get; set; } = 0m;     // Ham Başarı Notu Alt Limiti2
    public decimal FinalExamFloor { get; set; } = 0m;     // Yarıyıl Sonu Sınav Limiti

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
