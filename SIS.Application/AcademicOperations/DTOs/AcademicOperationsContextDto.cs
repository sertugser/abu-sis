namespace SIS.Application.AcademicOperations.DTOs;

public class AcademicOperationsContextDto
{
    public string RoleLabel { get; set; } = string.Empty;
    public bool CanViewAllDepartments { get; set; }
    public int? DefaultDepartmentId { get; set; }
    public List<AccessibleDepartmentDto> AccessibleDepartments { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}
