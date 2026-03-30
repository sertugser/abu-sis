using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AdminInstructorService : IAdminInstructorService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminInstructorService(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<List<AdminInstructorListItemDto>> GetInstructorsAsync(AdminInstructorFilterDto filter)
    {
        var instructors = await _context.Instructors
            .Include(i => i.Department)
            .ToListAsync();

        var userIds = instructors.Select(i => i.UserId).ToList();
        var users = await _context.Set<ApplicationUser>()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var result = new List<AdminInstructorListItemDto>();

        foreach (var instructor in instructors)
        {
            users.TryGetValue(instructor.UserId, out var user);

            var isActive = user != null && (user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow);

            if (filter.IsActive.HasValue && filter.IsActive.Value != isActive) continue;

            if (filter.DepartmentId.HasValue && instructor.DepartmentId != filter.DepartmentId.Value) continue;

            if (!string.IsNullOrWhiteSpace(filter.Title) &&
                !instructor.Title.Contains(filter.Title, StringComparison.OrdinalIgnoreCase)) continue;

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.ToLower();
                if (user == null ||
                    (!user.FirstName.ToLower().Contains(s) &&
                     !user.LastName.ToLower().Contains(s) &&
                     !(user.Email ?? "").ToLower().Contains(s)))
                    continue;
            }

            result.Add(new AdminInstructorListItemDto(
                instructor.Id,
                user?.Email ?? "",
                user?.FirstName ?? "",
                user?.LastName ?? "",
                instructor.Title,
                instructor.Department?.Name ?? "-",
                instructor.Department?.Code ?? "-",
                isActive
            ));
        }

        return result.OrderBy(i => i.DepartmentCode).ThenBy(i => i.LastName).ToList();
    }

    public async Task<AdminInstructorDetailDto?> GetInstructorByIdAsync(int id)
    {
        var instructor = await _context.Instructors
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (instructor == null) return null;

        var user = await _context.Set<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.Id == instructor.UserId);

        var isActive = user != null && (user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow);
        var roles = user != null
            ? (await _userManager.GetRolesAsync(user)).ToList()
            : new List<string>();

        return new AdminInstructorDetailDto(
            instructor.Id,
            user?.Email ?? "",
            user?.FirstName ?? "",
            user?.LastName ?? "",
            instructor.Title,
            instructor.Department?.Name ?? "-",
            instructor.Department?.Code ?? "-",
            instructor.MaxWeeklyHours,
            isActive,
            roles
        );
    }

    public async Task<AdminInstructorDetailDto> CreateInstructorAsync(CreateInstructorDto dto)
    {
        var department = await _context.Departments.FindAsync(dto.DepartmentId)
            ?? throw new KeyNotFoundException($"Bölüm bulunamadı: {dto.DepartmentId}");

        var email = dto.OverrideEmail ?? GenerateInstructorEmail(dto.FirstName, dto.LastName);

        int suffix = 1;
        var baseEmail = email;
        while (await _userManager.FindByEmailAsync(email) != null)
        {
            var atIdx = baseEmail.IndexOf('@');
            email = $"{baseEmail[..atIdx]}{suffix++}{baseEmail[atIdx..]}";
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, "Instructor");

        var instructor = new Instructor
        {
            UserId = user.Id,
            DepartmentId = dto.DepartmentId,
            Title = dto.Title,
            MaxWeeklyHours = dto.MaxWeeklyHours
        };
        _context.Instructors.Add(instructor);
        await _context.SaveChangesAsync();

        return (await GetInstructorByIdAsync(instructor.Id))!;
    }

    private static string GenerateInstructorEmail(string firstName, string lastName)
    {
        var first = Normalize(firstName);
        var last = Normalize(lastName);
        return $"{first}.{last}@ankarabilim.edu.tr";
    }

    private static string Normalize(string name) =>
        name.ToLower()
            .Replace(' ', '.')
            .Replace('ç', 'c').Replace('ğ', 'g').Replace('ı', 'i')
            .Replace('ö', 'o').Replace('ş', 's').Replace('ü', 'u')
            .Replace('â', 'a').Replace('î', 'i').Replace('û', 'u');
}
