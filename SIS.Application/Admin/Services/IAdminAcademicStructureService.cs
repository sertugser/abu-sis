using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Services;

public interface IAdminAcademicStructureService
{
    // Faculty
    Task<List<FacultyDto>> GetFacultiesAsync();
    Task<FacultyDto> CreateFacultyAsync(CreateFacultyDto dto);
    Task<FacultyDto> UpdateFacultyAsync(int id, UpdateFacultyDto dto);
    Task DeleteFacultyAsync(int id);

    // Department
    Task<List<DepartmentDto>> GetDepartmentsAsync(int? facultyId = null);
    Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentDto dto);
    Task<DepartmentDto> UpdateDepartmentAsync(int id, UpdateDepartmentDto dto);
    Task DeleteDepartmentAsync(int id);

    // Program
    Task<List<ProgramDto>> GetProgramsAsync(int? departmentId = null);
    Task<ProgramDto> CreateProgramAsync(CreateProgramDto dto);
    Task<ProgramDto> UpdateProgramAsync(int id, UpdateProgramDto dto);
    Task DeleteProgramAsync(int id);
}
