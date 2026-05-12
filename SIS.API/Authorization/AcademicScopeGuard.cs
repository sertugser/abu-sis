using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Authorization;

public sealed record AcademicActorScope(
    bool IsGlobal,
    bool IsForbidden,
    int? FacultyId,
    int? DepartmentId,
    IReadOnlyList<int> DepartmentIds);

public interface IAcademicScopeGuard
{
    Task<AcademicActorScope> ResolveAsync(ClaimsPrincipal user, int userId, CancellationToken ct = default);
    Task<bool> CanAccessFacultyAsync(ClaimsPrincipal user, int userId, int facultyId, CancellationToken ct = default);
    Task<bool> CanAccessDepartmentAsync(ClaimsPrincipal user, int userId, int departmentId, CancellationToken ct = default);
}

public sealed class AcademicScopeGuard : IAcademicScopeGuard
{
    private const string SystemAdministratorRole = "SystemAdministrator";
    private const string AdministrativeStaffRole = "AdministrativeStaff";
    private const string DeanRole = "Dean";
    private const string DepartmentManagerRole = "DepartmentManager";

    private readonly AppDbContext _context;

    public AcademicScopeGuard(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AcademicActorScope> ResolveAsync(ClaimsPrincipal user, int userId, CancellationToken ct = default)
    {
        var roles = user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal);

        if (roles.Contains(SystemAdministratorRole) || roles.Contains(AdministrativeStaffRole))
        {
            return new AcademicActorScope(
                IsGlobal: true,
                IsForbidden: false,
                FacultyId: null,
                DepartmentId: null,
                DepartmentIds: []);
        }

        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(item => item.Department)
            .FirstOrDefaultAsync(item => item.UserId == userId, ct);

        if (instructor?.Department == null)
        {
            return Forbidden();
        }

        if (roles.Contains(DeanRole))
        {
            var departmentIds = await _context.Departments
                .AsNoTracking()
                .Where(department => department.FacultyId == instructor.Department.FacultyId)
                .Select(department => department.Id)
                .ToListAsync(ct);

            return new AcademicActorScope(
                IsGlobal: false,
                IsForbidden: false,
                FacultyId: instructor.Department.FacultyId,
                DepartmentId: instructor.DepartmentId,
                DepartmentIds: departmentIds);
        }

        if (roles.Contains(DepartmentManagerRole))
        {
            return new AcademicActorScope(
                IsGlobal: false,
                IsForbidden: false,
                FacultyId: instructor.Department.FacultyId,
                DepartmentId: instructor.DepartmentId,
                DepartmentIds: [instructor.DepartmentId]);
        }

        return Forbidden();
    }

    public async Task<bool> CanAccessFacultyAsync(ClaimsPrincipal user, int userId, int facultyId, CancellationToken ct = default)
    {
        var scope = await ResolveAsync(user, userId, ct);
        if (scope.IsForbidden) return false;
        return scope.IsGlobal || scope.FacultyId == facultyId;
    }

    public async Task<bool> CanAccessDepartmentAsync(ClaimsPrincipal user, int userId, int departmentId, CancellationToken ct = default)
    {
        var scope = await ResolveAsync(user, userId, ct);
        if (scope.IsForbidden) return false;
        return scope.IsGlobal || scope.DepartmentIds.Contains(departmentId);
    }

    private static AcademicActorScope Forbidden() => new(
        IsGlobal: false,
        IsForbidden: true,
        FacultyId: null,
        DepartmentId: null,
        DepartmentIds: []);
}
