namespace SIS.Domain.Entities;

/// <summary>
/// Program bazlı sınıf atlatma kuralları. Admin tarafından tanımlanır.
/// Örnek: 2. sınıfa geçmek için min 1.80 CGPA, min 60 AKTS gerekir.
///
/// Hibrit scope (γ): CurriculumId null → program-bazlı (tüm müfredatlara default uygulanır).
/// CurriculumId set → o müfredata özgü override (program kuralı yerine geçer).
/// AcademicProgressionService önce CurriculumId-spesifik kuralı arar, yoksa program-bazlıya düşer.
/// </summary>
public class PromotionRule
{
    public int Id { get; set; }
    public int ProgramId { get; set; }
    public AcademicProgram Program { get; set; } = null!;
    /// <summary>Opsiyonel müfredat-özel override. Null = program-bazlı default kural.</summary>
    public int? CurriculumId { get; set; }
    public Curriculum? Curriculum { get; set; }
    public int FromYear { get; set; }       // Mevcut sınıf (1 = 1. sınıf)
    public int ToYear { get; set; }         // Hedef sınıf (2 = 2. sınıf)
    public decimal? MinCgpa { get; set; }
    public int? MinEcts { get; set; }
    public int? MinCourseCount { get; set; }
    public int? MaxFailedCourses { get; set; }          // Max FF ders sayısı (aşarsa sınıf atlayamaz)
    public int? MinCompulsoryCoursePassed { get; set; }  // O sınıfın zorunlu derslerinden min kaçı geçilmiş olmalı
}
