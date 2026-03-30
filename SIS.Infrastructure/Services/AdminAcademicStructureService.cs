using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AdminAcademicStructureService(AppDbContext db) : IAdminAcademicStructureService
{
    // ─── Faculty ───────────────────────────────────────────────────

    public async Task<List<FacultyDto>> GetFacultiesAsync()
    {
        return await db.Faculties
            .OrderBy(f => f.Name)
            .Select(f => new FacultyDto(
                f.Id, f.Name, f.Code,
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

    // ─── Department ────────────────────────────────────────────────

    public async Task<List<DepartmentDto>> GetDepartmentsAsync(int? facultyId = null)
    {
        var query = db.Departments.Include(d => d.Faculty).AsQueryable();
        if (facultyId.HasValue) query = query.Where(d => d.FacultyId == facultyId.Value);

        return await query
            .OrderBy(d => d.Faculty.Name).ThenBy(d => d.Name)
            .Select(d => new DepartmentDto(
                d.Id, d.Name, d.Code, d.FacultyId, d.Faculty.Name,
                d.Programs.Count))
            .ToListAsync();
    }

    public async Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentDto dto)
    {
        if (!await db.Faculties.AnyAsync(f => f.Id == dto.FacultyId))
            throw new KeyNotFoundException("Fakülte bulunamadı.");
        if (await db.Departments.AnyAsync(d => d.Code == dto.Code))
            throw new InvalidOperationException($"'{dto.Code}' kodlu bölüm zaten mevcut.");

        var dept = new Department { Name = dto.Name, Code = dto.Code, FacultyId = dto.FacultyId };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();

        var facultyName = (await db.Faculties.FindAsync(dto.FacultyId))!.Name;
        return new DepartmentDto(dept.Id, dept.Name, dept.Code, dept.FacultyId, facultyName, 0);
    }

    public async Task<DepartmentDto> UpdateDepartmentAsync(int id, UpdateDepartmentDto dto)
    {
        var dept = await db.Departments.Include(d => d.Faculty).FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException("Bölüm bulunamadı.");
        if (!await db.Faculties.AnyAsync(f => f.Id == dto.FacultyId))
            throw new KeyNotFoundException("Fakülte bulunamadı.");
        if (await db.Departments.AnyAsync(d => d.Code == dto.Code && d.Id != id))
            throw new InvalidOperationException($"'{dto.Code}' kodu başka bir bölüm tarafından kullanılıyor.");

        dept.Name = dto.Name;
        dept.Code = dto.Code;
        dept.FacultyId = dto.FacultyId;
        await db.SaveChangesAsync();

        var progCount = await db.AcademicPrograms.CountAsync(p => p.DepartmentId == id);
        return new DepartmentDto(dept.Id, dept.Name, dept.Code, dept.FacultyId, dept.Faculty.Name, progCount);
    }

    public async Task DeleteDepartmentAsync(int id)
    {
        if (await db.AcademicPrograms.AnyAsync(p => p.DepartmentId == id))
            throw new InvalidOperationException("Bu bölüme bağlı programlar var. Önce programları silin.");
        if (await db.Instructors.AnyAsync(i => i.DepartmentId == id))
            throw new InvalidOperationException("Bu bölüme atanmış öğretim üyeleri var.");

        var dept = await db.Departments.FindAsync(id)
            ?? throw new KeyNotFoundException("Bölüm bulunamadı.");
        db.Departments.Remove(dept);
        await db.SaveChangesAsync();
    }

    // ─── Program ────────────────────────────────────────────────────

    public async Task<List<ProgramDto>> GetProgramsAsync(int? departmentId = null)
    {
        var query = db.AcademicPrograms.Include(p => p.Department).AsQueryable();
        if (departmentId.HasValue) query = query.Where(p => p.DepartmentId == departmentId.Value);

        return await query
            .OrderBy(p => p.Department.Name).ThenBy(p => p.Name)
            .Select(p => new ProgramDto(
                p.Id, p.Name, p.Code,
                p.DepartmentId, p.Department.Name,
                p.DegreeLevel.ToString(), p.EducationType.ToString(),
                p.MaxStudyDurationSemesters, p.TotalRequiredEcts,
                p.Curricula.Count))
            .ToListAsync();
    }

    public async Task<ProgramDto> CreateProgramAsync(CreateProgramDto dto)
    {
        if (!await db.Departments.AnyAsync(d => d.Id == dto.DepartmentId))
            throw new KeyNotFoundException("Bölüm bulunamadı.");
        if (await db.AcademicPrograms.AnyAsync(p => p.Code == dto.Code))
            throw new InvalidOperationException($"'{dto.Code}' kodlu program zaten mevcut.");

        var program = new AcademicProgram
        {
            Name = dto.Name,
            Code = dto.Code,
            DepartmentId = dto.DepartmentId,
            DegreeLevel = Enum.Parse<DegreeLevel>(dto.DegreeLevel),
            EducationType = Enum.Parse<EducationType>(dto.EducationType),
            MaxStudyDurationSemesters = dto.MaxStudyDurationSemesters,
            TotalRequiredEcts = dto.TotalRequiredEcts
        };
        db.AcademicPrograms.Add(program);
        await db.SaveChangesAsync();

        var deptName = (await db.Departments.FindAsync(dto.DepartmentId))!.Name;
        return new ProgramDto(program.Id, program.Name, program.Code,
            program.DepartmentId, deptName,
            program.DegreeLevel.ToString(), program.EducationType.ToString(),
            program.MaxStudyDurationSemesters, program.TotalRequiredEcts, 0);
    }

    public async Task<ProgramDto> UpdateProgramAsync(int id, UpdateProgramDto dto)
    {
        var program = await db.AcademicPrograms.Include(p => p.Department).FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new KeyNotFoundException("Program bulunamadı.");
        if (!await db.Departments.AnyAsync(d => d.Id == dto.DepartmentId))
            throw new KeyNotFoundException("Bölüm bulunamadı.");
        if (await db.AcademicPrograms.AnyAsync(p => p.Code == dto.Code && p.Id != id))
            throw new InvalidOperationException($"'{dto.Code}' kodu başka bir program tarafından kullanılıyor.");

        program.Name = dto.Name;
        program.Code = dto.Code;
        program.DepartmentId = dto.DepartmentId;
        program.DegreeLevel = Enum.Parse<DegreeLevel>(dto.DegreeLevel);
        program.EducationType = Enum.Parse<EducationType>(dto.EducationType);
        program.MaxStudyDurationSemesters = dto.MaxStudyDurationSemesters;
        program.TotalRequiredEcts = dto.TotalRequiredEcts;
        await db.SaveChangesAsync();

        var currCount = await db.Curricula.CountAsync(c => c.ProgramId == id);
        return new ProgramDto(program.Id, program.Name, program.Code,
            program.DepartmentId, program.Department.Name,
            program.DegreeLevel.ToString(), program.EducationType.ToString(),
            program.MaxStudyDurationSemesters, program.TotalRequiredEcts, currCount);
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
