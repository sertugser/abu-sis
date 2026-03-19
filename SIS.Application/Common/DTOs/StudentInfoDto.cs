namespace SIS.Application.Common.DTOs;

public class StudentInfoDto
{
    public int Id { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public decimal Cgpa { get; set; }
    public decimal Sgpa { get; set; }
    public int CompletedSemesters { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsScholarshipStudent { get; set; }
    public bool HasFinancialBlock { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string FacultyName { get; set; } = string.Empty;
    public int WithdrawalCount { get; set; }
    public int ProgramId { get; set; }
}
