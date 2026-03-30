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
        var allUsers = await _userManager.Users.ToListAsync();

        // Determine active/locked status via LockoutEnd
        var result = new List<AdminUserListItemDto>();
        foreach (var user in allUsers)
        {
            var roles = (await _userManager.GetRolesAsync(user)).ToList();

            if (!string.IsNullOrWhiteSpace(filter.Role) && !roles.Contains(filter.Role))
                continue;

            var isActive = !await _userManager.IsLockedOutAsync(user);

            if (filter.IsActive.HasValue && filter.IsActive.Value != isActive)
                continue;

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.ToLower();
                if (!user.FirstName.ToLower().Contains(s) &&
                    !user.LastName.ToLower().Contains(s) &&
                    !(user.Email ?? "").ToLower().Contains(s))
                    continue;
            }

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
