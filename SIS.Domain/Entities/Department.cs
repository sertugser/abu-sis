namespace SIS.Domain.Entities;

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int FacultyId { get; set; }
    public Faculty Faculty { get; set; } = null!;

    /// <summary>
    /// Koordinatörlük birimi (öğrencisi yok, sadece ders/hoca sahipliği yapar).
    /// Örn: Temel Bilimler Koordinatörlüğü, AİİT Koordinatörlüğü, Türk Dili Bölümü, Yabancı Diller YO.
    /// Ortak dersler (MATH, FIZ, AIIT, TRD, YDL) bu tipteki birimlere atanır.
    /// </summary>
    public bool IsServiceUnit { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ICollection<AcademicProgram> Programs { get; set; } = new List<AcademicProgram>();
}
