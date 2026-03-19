using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class Classroom
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public ClassroomType Type { get; set; }
}
