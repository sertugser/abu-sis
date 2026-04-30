using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AdminAcademicStructureService(AppDbContext db) : IAdminAcademicStructureService
{
    private static string NormalizeRequiredText(string value, string fieldName)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException($"{fieldName} boş bırakılamaz.");

        return normalized;
    }

    private static string NormalizeCode(string value, string fieldName)
    {
        return NormalizeRequiredText(value, fieldName).ToUpperInvariant();
    }

    private static string? NormalizeOptionalCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToUpperInvariant();
    }

    public async Task<List<FacultyDto>> GetFacultiesAsync()
    {
        return await db.Faculties
            .OrderBy(f => f.Name)
            .Select(f => new FacultyDto(
                f.Id,
                f.Name,
                f.Code,
                f.Departments.Count))
            .ToListAsync();
    }

    public async Task<FacultyDto> CreateFacultyAsync(CreateFacultyDto dto)
    {
        if (await db.Faculties.AnyAsync(f => f.Code == dto.Code))
            throw new InvalidOperationException($"'{dto.Code}' kodlu fakülte zaten mevcut.");

        var faculty = new Faculty { Name = dto.Name, Code = dto.Code };
        db.Faculties.Add(faculty);
        await db.SaveChangesAsync();
        return new FacultyDto(faculty.Id, faculty.Name, faculty.Code, 0);
    }

    public async Task<FacultyDto> UpdateFacultyAsync(int id, UpdateFacultyDto dto)
    {
        var faculty = await db.Faculties.FindAsync(id)
            ?? throw new KeyNotFoundException("Fakülte bulunamadı.");
        if (await db.Faculties.AnyAsync(f => f.Code == dto.Code && f.Id != id))
            throw new InvalidOperationException($"'{dto.Code}' kodu başka bir fakülte tarafından kullanılıyor.");

        faculty.Name = dto.Name;
        faculty.Code = dto.Code;
        await db.SaveChangesAsync();
        var deptCount = await db.Departments.CountAsync(d => d.FacultyId == id);
        return new FacultyDto(faculty.Id, faculty.Name, faculty.Code, deptCount);
    }

    public async Task DeleteFacultyAsync(int id)
    {
        if (await db.Departments.AnyAsync(d => d.FacultyId == id))
            throw new InvalidOperationException("Bu fakülteye bağlı bölümler var. Önce bölümleri silin.");

        var faculty = await db.Faculties.FindAsync(id)
            ?? throw new KeyNotFoundException("Fakülte bulunamadı.");
        db.Faculties.Remove(faculty);
        await db.SaveChangesAsync();
    }

    public async Task<List<DepartmentDto>> GetDepartmentsAsync(int? facultyId = null)
    {
        var query = db.Departments.Include(d => d.Faculty).AsQueryable();
        if (facultyId.HasValue)
            query = query.Where(d => d.FacultyId == facultyId.Value);

        var instructorCounts = await db.Instructors
            .GroupBy(i => i.DepartmentId)
            .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DepartmentId, x => x.Count);

        var studentCounts = await db.StudentPrograms
            .GroupBy(sp => sp.Program.DepartmentId)
            .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DepartmentId, x => x.Count);

        var rows = await query
            .OrderBy(d => d.Faculty.Name)
            .ThenBy(d => d.Name)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Code,
                d.FacultyId,
                FacultyName = d.Faculty.Name,
                ProgramCount = d.Programs.Count,
                d.IsServiceUnit
            })
            .ToListAsync();

        return rows.Select(d => new DepartmentDto(
            d.Id,
            d.Name,
            d.Code,
            d.FacultyId,
            d.FacultyName,
            d.ProgramCount,
            studentCounts.TryGetValue(d.Id, out var studentCount) ? studentCount : 0,
            instructorCounts.TryGetValue(d.Id, out var instructorCount) ? instructorCount : 0,
            d.IsServiceUnit)).ToList();
    }

    public async Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentDto dto)
    {
        if (!await db.Faculties.AnyAsync(f => f.Id == dto.FacultyId))
            throw new KeyNotFoundException("Fakülte bulunamadı.");
        if (await db.Departments.AnyAsync(d => d.Code == dto.Code))
            throw new InvalidOperationException($"'{dto.Code}' kodlu bölüm zaten mevcut.");

        var department = new Department
        {
            Name = dto.Name,
            Code = dto.Code,
            FacultyId = dto.FacultyId
        };

        db.Departments.Add(department);
        await db.SaveChangesAsync();

        var facultyName = (await db.Faculties.FindAsync(dto.FacultyId))!.Name;
        return new DepartmentDto(
            department.Id,
            department.Name,
            department.Code,
            department.FacultyId,
            facultyName,
            ProgramCount: 0,
            StudentCount: 0,
            InstructorCount: 0,
            IsServiceUnit: department.IsServiceUnit);
    }

    public async Task<DepartmentDto> UpdateDepartmentAsync(int id, UpdateDepartmentDto dto)
    {
        var department = await db.Departments.Include(d => d.Faculty).FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException("Bölüm bulunamadı.");
        if (!await db.Faculties.AnyAsync(f => f.Id == dto.FacultyId))
            throw new KeyNotFoundException("Fakülte bulunamadı.");
        if (await db.Departments.AnyAsync(d => d.Code == dto.Code && d.Id != id))
            throw new InvalidOperationException($"'{dto.Code}' kodu başka bir bölüm tarafından kullanılıyor.");

        department.Name = dto.Name;
        department.Code = dto.Code;
        department.FacultyId = dto.FacultyId;
        await db.SaveChangesAsync();

        var programCount = await db.AcademicPrograms.CountAsync(p => p.DepartmentId == id);
        var instructorCount = await db.Instructors.CountAsync(i => i.DepartmentId == id);
        var studentCount = await db.StudentPrograms.CountAsync(sp => sp.Program.DepartmentId == id);
        return new DepartmentDto(
            department.Id,
            department.Name,
            department.Code,
            department.FacultyId,
            department.Faculty.Name,
            programCount,
            studentCount,
            instructorCount,
            department.IsServiceUnit);
    }

    public async Task DeleteDepartmentAsync(int id)
    {
        if (await db.AcademicPrograms.AnyAsync(p => p.DepartmentId == id))
            throw new InvalidOperationException("Bu bölüme bağlı programlar var. Önce programları silin.");
        if (await db.Instructors.AnyAsync(i => i.DepartmentId == id))
            throw new InvalidOperationException("Bu bölüme atanmış öğretim üyeleri var.");

        var department = await db.Departments.FindAsync(id)
            ?? throw new KeyNotFoundException("Bölüm bulunamadı.");
        db.Departments.Remove(department);
        await db.SaveChangesAsync();
    }

    public async Task<List<ProgramDto>> GetProgramsAsync(int? departmentId = null)
    {
        var query = db.AcademicPrograms.Include(p => p.Department).AsQueryable();
        if (departmentId.HasValue)
            query = query.Where(p => p.DepartmentId == departmentId.Value);

        return await query
            .OrderBy(p => p.Department.Name)
            .ThenBy(p => p.Name)
            .Select(p => new ProgramDto(
                p.Id,
                p.Name,
                p.Code,
                p.ExternalSystemCode,
                p.DepartmentId,
                p.Department.Name,
                p.DegreeLevel.ToString(),
                p.EducationType.ToString(),
                p.MaxStudyDurationSemesters,
                p.TotalRequiredEcts,
                p.Curricula.Count))
            .ToListAsync();
    }

    public async Task<ProgramDto> CreateProgramAsync(CreateProgramDto dto)
    {
        var programName = NormalizeRequiredText(dto.Name, "Program adı");
        var programCode = NormalizeCode(dto.Code, "Program kodu");
        var externalSystemCode = NormalizeOptionalCode(dto.ExternalSystemCode);

        if (!await db.Departments.AnyAsync(d => d.Id == dto.DepartmentId))
            throw new KeyNotFoundException("Bölüm bulunamadı.");
        if (await db.AcademicPrograms.AnyAsync(p => p.Code == programCode))
            throw new InvalidOperationException($"'{programCode}' kodlu program zaten mevcut.");
        if (externalSystemCode is not null &&
            await db.AcademicPrograms.AnyAsync(p => p.ExternalSystemCode == externalSystemCode))
            throw new InvalidOperationException($"'{externalSystemCode}' dış sistem kodu başka bir program tarafından kullanılıyor.");

        var program = new AcademicProgram
        {
            Name = programName,
            Code = programCode,
            ExternalSystemCode = externalSystemCode,
            DepartmentId = dto.DepartmentId,
            DegreeLevel = Enum.Parse<DegreeLevel>(dto.DegreeLevel),
            EducationType = Enum.Parse<EducationType>(dto.EducationType),
            MaxStudyDurationSemesters = dto.MaxStudyDurationSemesters,
            TotalRequiredEcts = dto.TotalRequiredEcts
        };

        db.AcademicPrograms.Add(program);
        await db.SaveChangesAsync();

        var departmentName = (await db.Departments.FindAsync(dto.DepartmentId))!.Name;
        return new ProgramDto(
            program.Id,
            program.Name,
            program.Code,
            program.ExternalSystemCode,
            program.DepartmentId,
            departmentName,
            program.DegreeLevel.ToString(),
            program.EducationType.ToString(),
            program.MaxStudyDurationSemesters,
            program.TotalRequiredEcts,
            0);
    }

    public async Task<ProgramDto> UpdateProgramAsync(int id, UpdateProgramDto dto)
    {
        var programName = NormalizeRequiredText(dto.Name, "Program adı");
        var programCode = NormalizeCode(dto.Code, "Program kodu");
        var externalSystemCode = NormalizeOptionalCode(dto.ExternalSystemCode);

        var program = await db.AcademicPrograms.Include(p => p.Department).FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new KeyNotFoundException("Program bulunamadı.");
        if (!await db.Departments.AnyAsync(d => d.Id == dto.DepartmentId))
            throw new KeyNotFoundException("Bölüm bulunamadı.");
        if (await db.AcademicPrograms.AnyAsync(p => p.Code == programCode && p.Id != id))
            throw new InvalidOperationException($"'{programCode}' kodu başka bir program tarafından kullanılıyor.");
        if (externalSystemCode is not null &&
            await db.AcademicPrograms.AnyAsync(p => p.ExternalSystemCode == externalSystemCode && p.Id != id))
            throw new InvalidOperationException($"'{externalSystemCode}' dış sistem kodu başka bir program tarafından kullanılıyor.");

        program.Name = programName;
        program.Code = programCode;
        program.ExternalSystemCode = externalSystemCode;
        program.DepartmentId = dto.DepartmentId;
        program.DegreeLevel = Enum.Parse<DegreeLevel>(dto.DegreeLevel);
        program.EducationType = Enum.Parse<EducationType>(dto.EducationType);
        program.MaxStudyDurationSemesters = dto.MaxStudyDurationSemesters;
        program.TotalRequiredEcts = dto.TotalRequiredEcts;
        await db.SaveChangesAsync();

        var curriculumCount = await db.Curricula.CountAsync(c => c.ProgramId == id);
        return new ProgramDto(
            program.Id,
            program.Name,
            program.Code,
            program.ExternalSystemCode,
            program.DepartmentId,
            program.Department.Name,
            program.DegreeLevel.ToString(),
            program.EducationType.ToString(),
            program.MaxStudyDurationSemesters,
            program.TotalRequiredEcts,
            curriculumCount);
    }

    public async Task DeleteProgramAsync(int id)
    {
        if (await db.StudentPrograms.AnyAsync(sp => sp.ProgramId == id))
            throw new InvalidOperationException("Bu programa kayıtlı öğrenciler var. Program silinemez.");

        var program = await db.AcademicPrograms.FindAsync(id)
            ?? throw new KeyNotFoundException("Program bulunamadı.");
        db.AcademicPrograms.Remove(program);
        await db.SaveChangesAsync();
    }
}
