using Microsoft.EntityFrameworkCore;
using SIS.Application.Identity.Exceptions;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Persistence.Extensions;

namespace SIS.Infrastructure.Services.Scope;

public interface IScopeResolver
{
    Task<int> ResolveFacultyFromDepartmentAsync(int departmentId, CancellationToken ct = default);
    Task<int> ResolveFacultyFromProgramAsync(int programId, CancellationToken ct = default);
    Task<int> ResolveFacultyFromCourseAsync(int courseId, CancellationToken ct = default);
}

public class ScopeResolver : IScopeResolver
{
    private readonly AppDbContext _db;

    public ScopeResolver(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> ResolveFacultyFromDepartmentAsync(int departmentId, CancellationToken ct = default)
    {
        var facultyId = await _db.Departments
            .WithoutScope()
            .Where(department => department.Id == departmentId)
            .Select(department => (int?)department.FacultyId)
            .FirstOrDefaultAsync(ct);

        return facultyId ?? throw new ScopeViolationException($"Department {departmentId} not found.");
    }

    public async Task<int> ResolveFacultyFromProgramAsync(int programId, CancellationToken ct = default)
    {
        var facultyId = await _db.AcademicPrograms
            .WithoutScope()
            .Where(program => program.Id == programId)
            .Select(program => (int?)program.Department.FacultyId)
            .FirstOrDefaultAsync(ct);

        return facultyId ?? throw new ScopeViolationException($"Program {programId} not found.");
    }

    public async Task<int> ResolveFacultyFromCourseAsync(int courseId, CancellationToken ct = default)
    {
        var facultyId = await _db.Courses
            .WithoutScope()
            .Where(course => course.Id == courseId)
            .Select(course => course.DepartmentId == null ? null : (int?)course.Department!.FacultyId)
            .FirstOrDefaultAsync(ct);

        return facultyId ?? throw new ScopeViolationException($"Course {courseId} is not bound to a faculty.");
    }
}
