using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AdminUserService : IAdminUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;

    public AdminUserService(UserManager<ApplicationUser> userManager, AppDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public async Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(AdminUserFilterDto filter)
    {
        // Batch load all role mappings in TWO queries instead of N+1 per-user GetRolesAsync calls
        var allUserRoles = await _context.UserRoles.ToListAsync();
        var roleNamesById = await _context.Roles.ToDictionaryAsync(r => r.Id, r => r.Name ?? "");

        var rolesByUserId = allUserRoles
            .GroupBy(ur => ur.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(ur => roleNamesById.GetValueOrDefault(ur.RoleId, ""))
                      .Where(n => !string.IsNullOrEmpty(n))
                      .ToList());

        // Build base query with DB-level search filtering
        var query = _userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(s) ||
                u.LastName.ToLower().Contains(s) ||
                (u.Email != null && u.Email.ToLower().Contains(s)));
        }

        // IsActive filter: locked out users have LockoutEnd > now
        if (filter.IsActive.HasValue)
        {
            var now = DateTimeOffset.UtcNow;
            if (filter.IsActive.Value)
                query = query.Where(u => !u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= now);
            else
                query = query.Where(u => u.LockoutEnabled && u.LockoutEnd != null && u.LockoutEnd > now);
        }

        // Materialize filtered users
        var filteredUsers = await query.ToListAsync();

        // Apply role filter in memory (roles are already batch-loaded)
        var result = new List<AdminUserListItemDto>();
        var now2 = DateTimeOffset.UtcNow;

        foreach (var user in filteredUsers)
        {
            var roles = rolesByUserId.GetValueOrDefault(user.Id, new List<string>());

            if (!string.IsNullOrWhiteSpace(filter.Role) && !roles.Contains(filter.Role))
                continue;

            var isActive = !user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd <= now2;

            result.Add(new AdminUserListItemDto(
                user.Id,
                user.Email ?? "",
                user.FirstName,
                user.LastName,
                $"{user.FirstName} {user.LastName}",
                roles,
                isActive,
                ResolveUserType(roles)
            ));
        }

        var ordered = result
            .OrderBy(u => u.UserType)
            .ThenBy(u => u.FullName)
            .ToList();

        var totalCount = ordered.Count;
        var items = ordered
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return new PagedResult<AdminUserListItemDto>(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<AdminUserDetailDto> GetUserByIdAsync(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new KeyNotFoundException($"Kullanıcı bulunamadı: {id}");

        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        var isActive = !await _userManager.IsLockedOutAsync(user);
        var userType = ResolveUserType(roles);

        string? studentNumber = null;
        string? instructorTitle = null;
        string? department = null;

        if (userType == "Student")
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.UserId == id);
            studentNumber = student?.StudentNumber;
        }
        else if (userType == "Instructor")
        {
            var instructor = await _context.Instructors
                .Include(i => i.Department)
                .FirstOrDefaultAsync(i => i.UserId == id);
            instructorTitle = instructor?.Title;
            department = instructor?.Department?.Name;
        }

        return new AdminUserDetailDto(
            user.Id,
            user.Email ?? "",
            user.FirstName,
            user.LastName,
            $"{user.FirstName} {user.LastName}",
            roles,
            isActive,
            userType,
            studentNumber,
            instructorTitle,
            department
        );
    }

    public async Task UpdateUserRolesAsync(int id, UpdateUserRolesDto dto)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new KeyNotFoundException($"Kullanıcı bulunamadı: {id}");

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);

        if (dto.Roles.Count > 0)
            await _userManager.AddToRolesAsync(user, dto.Roles);
    }

    public async Task ResetPasswordAsync(int id, AdminResetPasswordDto dto)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new KeyNotFoundException($"Kullanıcı bulunamadı: {id}");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);

        if (!result.Succeeded)
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));

        user.PasswordChangedAtUtc = DateTime.UtcNow;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            throw new InvalidOperationException(
                string.Join(", ", updateResult.Errors.Select(e => e.Description)));
    }

    public async Task SetUserStatusAsync(int id, bool isActive)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new KeyNotFoundException($"Kullanıcı bulunamadı: {id}");

        if (isActive)
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
        }
        else
        {
            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        }
    }

    private static string ResolveUserType(IList<string> roles)
    {
        if (roles.Contains("Student")) return "Student";
        if (roles.Contains("Instructor") || roles.Contains("AcademicAdvisor")) return "Instructor";
        if (roles.Contains("SystemAdministrator") || roles.Contains("Dean") ||
            roles.Contains("DepartmentManager") || roles.Contains("AdministrativeStaff"))
            return "Admin";
        return "Other";
    }
}
