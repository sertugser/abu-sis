namespace SIS.Application.Admin.DTOs;

public record ProgramQuotaDto(
    int Id,
    int ProgramId,
    string ProgramName,
    string DepartmentName,
    string FacultyName,
    int Year,
    string Type,           // enum.ToString()
    string TypeLabel,      // "YKS" vb.
    int Quantity,
    string? Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CreateProgramQuotaDto(
    int ProgramId,
    int Year,
    string Type,
    int Quantity,
    string? Description
);

public record UpdateProgramQuotaDto(
    int Year,
    string Type,
    int Quantity,
    string? Description
);
