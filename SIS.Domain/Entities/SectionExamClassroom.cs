namespace SIS.Domain.Entities;

/// <summary>Bir sınav oturumunun atandığı derslik(ler) — çoklu (1..N).</summary>
public class SectionExamClassroom
{
    public int Id { get; set; }

    public int SectionExamId { get; set; }
    public SectionExam SectionExam { get; set; } = null!;

    public int ClassroomId { get; set; }
    public Classroom Classroom { get; set; } = null!;
}
