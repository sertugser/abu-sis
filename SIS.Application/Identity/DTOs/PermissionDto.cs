using SIS.Domain.Enums;

namespace SIS.Application.Identity.DTOs;

public class PermissionDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public PermissionCategory Category { get; set; }
    public string? Description { get; set; }
}
