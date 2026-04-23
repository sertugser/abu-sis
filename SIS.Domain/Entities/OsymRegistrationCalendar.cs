using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class OsymRegistrationCalendar
{
    public int Id { get; set; }
    public int Year { get; set; }
    public OsymRegistrationType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
