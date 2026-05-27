using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Authorization;

public sealed record AcademicActorScope(
    bool IsGlobal,
    bool IsForbidden,
    int? FacultyId,
    int? DepartmentId,
    IReadOnlyList<int> DepartmentIds,
    // Danışman scope'u departman değil, "kendi danışmanlık öğrencileri" bazlıdır.
    // Set ise: erişim s.AdvisorId == AdvisorInstructorId ile sınırlanır.
    int? AdvisorInstructorId = null);

public interface IAcademicScopeGuard
{
    Task<AcademicActorScope> ResolveAsync(ClaimsPrincipal user, int userId, CancellationToken ct = default);
    Task<bool> CanAccessFacultyAsync(ClaimsPrincipal user, int userId, int facultyId, CancellationToken ct = default);
    Task<bool> CanAccessDepartmentAsync(ClaimsPrincipal user, int userId, int departmentId, CancellationToken ct = default);
    Task<bool> CanAccessStudentAsync(ClaimsPrincipal user, int userId, int studentId, CancellationToken ct = default);
}

public sealed class AcademicScopeGuard : IAcademicScopeGuard
{
    private const string SystemAdministratorRole = "SystemAdministrator";
    private const string AdministrativeStaffRole = "AdministrativeStaff";
    private const string DeanRole = "Dean";
    private const string DepartmentManagerRole = "DepartmentManager";
    private const string AcademicAdvisorRole = "AcademicAdvisor";

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

        // Danışman: departman değil, kendi danışmanlık öğrencileriyle sınırlı erişim.
        if (roles.Contains(AcademicAdvisorRole))
        {
            return new AcademicActorScope(
                IsGlobal: false,
                IsForbidden: false,
                FacultyId: instructor.Department.FacultyId,
                DepartmentId: instructor.DepartmentId,
                DepartmentIds: [],
                AdvisorInstructorId: instructor.Id);
        }

        return Forbidden();
    }

    public async Task<bool> CanAccessStudentAsync(ClaimsPrincipal user, int userId, int studentId, CancellationToken ct = default)
    {
        var scope = await ResolveAsync(user, userId, ct);
        if (scope.IsForbidden) return false;
        if (scope.IsGlobal) return true;

        // Danışman: yalnızca kendi danışmanlık öğrencisi.
        if (scope.AdvisorInstructorId.HasValue)
        {
            return await _context.Students
                .AsNoTracking()
                .AnyAsync(s => s.Id == studentId && s.AdvisorId == scope.AdvisorInstructorId.Value, ct);
        }

        // Dean/DepartmentManager: öğrencinin ana program departmanı scope içinde mi?
        var studentDeptId = await _context.StudentPrograms
            .AsNoTracking()
            .Include(sp => sp.Program)
            .Where(sp => sp.StudentId == studentId && sp.IsActive && sp.Type == StudentProgramType.Main)
            .Select(sp => (int?)sp.Program.DepartmentId)
            .FirstOrDefaultAsync(ct);

        return studentDeptId.HasValue && scope.DepartmentIds.Contains(studentDeptId.Value);
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
