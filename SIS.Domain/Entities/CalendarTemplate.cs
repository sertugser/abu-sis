namespace SIS.Domain.Entities;

public class CalendarTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ChainCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string ItemsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
