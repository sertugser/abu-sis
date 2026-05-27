using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Services;

public interface IAdminAcademicStructureService
{
    // Faculty
    Task<List<FacultyDto>> GetFacultiesAsync();
    Task<FacultyDto> CreateFacultyAsync(CreateFacultyDto dto);
    Task<BulkImportResultDto> BulkCreateFacultiesAsync(IReadOnlyList<CreateFacultyDto> items);
    Task<FacultyDto> UpdateFacultyAsync(int id, UpdateFacultyDto dto);
    Task<FacultyDeletionSummaryDto> GetFacultyDeletionSummaryAsync(int id);
    Task<FacultyOverviewDto> GetFacultyOverviewAsync(int id);
    Task DeleteFacultyAsync(int id);

    // Department
    Task<List<DepartmentDto>> GetDepartmentsAsync(int? facultyId = null);
    Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentDto dto);
    Task<BulkImportResultDto> BulkCreateDepartmentsAsync(IReadOnlyList<CreateDepartmentDto> items);
    Task<DepartmentDto> UpdateDepartmentAsync(int id, UpdateDepartmentDto dto);
    Task<DepartmentOverviewDto> GetDepartmentOverviewAsync(int id);
    Task<DepartmentDeletionSummaryDto> GetDepartmentDeletionSummaryAsync(int id);
    Task DeleteDepartmentAsync(int id);

    // Program
    Task<List<ProgramDto>> GetProgramsAsync(int? departmentId = null);
    Task<ProgramDto> CreateProgramAsync(CreateProgramDto dto);
    Task<ProgramDto> UpdateProgramAsync(int id, UpdateProgramDto dto);
    Task<ProgramDeletionSummaryDto> GetProgramDeletionSummaryAsync(int id);
    Task DeleteProgramAsync(int id);
}
