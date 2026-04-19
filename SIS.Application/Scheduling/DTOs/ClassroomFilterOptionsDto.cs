namespace SIS.Application.Scheduling.DTOs;

public class ClassroomFilterOptionsDto
{
    public List<ClassroomFacultyOption> Faculties { get; set; } = new();
    public List<string> Buildings { get; set; } = new();
    public List<string> ClassroomTypes { get; set; } = new();
}

public class ClassroomFacultyOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
