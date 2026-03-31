namespace SIS.Domain.Entities;

/// <summary>
/// Dönemlik derslik-fakülte ataması. Her fakülte sadece kendine atanmış dersliklerden seçebilir.
/// </summary>
public class ClassroomTermAssignment
{
    public int Id { get; set; }
    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;
    public int ClassroomId { get; set; }
    public Classroom Classroom { get; set; } = null!;
    public int FacultyId { get; set; }
    public Faculty Faculty { get; set; } = null!;
}
