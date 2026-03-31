using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class Classroom
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public int Floor { get; set; }
    public int Capacity { get; set; }
    public ClassroomType Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
