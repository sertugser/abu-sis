using Microsoft.EntityFrameworkCore;
using SIS.Application.Announcement.DTOs;
using SIS.Application.Announcement.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Announcement;

public class InternalAnnouncementService : IInternalAnnouncementService
{
    private readonly AppDbContext _context;

    public InternalAnnouncementService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<AnnouncementListDto>> GetActiveForUserAsync(
        string userId,
        IEnumerable<string> roles,
        int? userDepartmentId,
        int? maxResults,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var roleSet = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var isStudent = roleSet.Contains("Student");
        var isInstructor = roleSet.Contains("Instructor") || roleSet.Contains("AcademicAdvisor");
        var isManagement = roleSet.Contains("Dean")
            || roleSet.Contains("DepartmentManager")
            || roleSet.Contains("SystemAdministrator")
            || roleSet.Contains("AdministrativeStaff");

        var query = _context.InternalAnnouncements
            .AsNoTracking()
            .Include(a => a.TargetDepartment)
            .Where(a => a.IsActive
                && a.StartsAt <= now
                && (a.ExpiresAt == null || a.ExpiresAt > now));

        query = query.Where(a =>
            a.TargetAudience == AnnouncementAudience.All
            || (a.TargetAudience == AnnouncementAudience.Students && isStudent)
            || (a.TargetAudience == AnnouncementAudience.Instructors && isInstructor)
            || (a.TargetAudience == AnnouncementAudience.Management && isManagement)
            || (a.TargetAudience == AnnouncementAudience.Department
                && userDepartmentId.HasValue
                && a.TargetDepartmentId == userDepartmentId));

        query = query
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.CreatedAt);

        if (maxResults.HasValue && maxResults.Value > 0)
            query = query.Take(maxResults.Value);

        var items = await query.ToListAsync(ct);
        return items.Select(MapToList).ToList();
    }

    public async Task<AnnouncementDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var announcement = await _context.InternalAnnouncements
            .AsNoTracking()
            .Include(a => a.TargetDepartment)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (announcement == null)
            return null;

        return MapToDetail(announcement);
    }

    public async Task<List<AnnouncementListDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await _context.InternalAnnouncements
            .AsNoTracking()
            .Include(a => a.TargetDepartment)
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return items.Select(MapToList).ToList();
    }

    public async Task<AnnouncementDetailDto> CreateAsync(
        CreateAnnouncementDto dto,
        string createdBy,
        string createdByName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new InvalidOperationException("Duyuru basligi bos olamaz.");
        if (string.IsNullOrWhiteSpace(dto.Content))
            throw new InvalidOperationException("Duyuru icerigi bos olamaz.");
        if (dto.TargetAudience == AnnouncementAudience.Department && !dto.TargetDepartmentId.HasValue)
            throw new InvalidOperationException("Bolum hedefi secilmeli.");

        if (dto.TargetDepartmentId.HasValue)
        {
            var exists = await _context.Departments.AnyAsync(d => d.Id == dto.TargetDepartmentId.Value, ct);
            if (!exists)
                throw new InvalidOperationException("Bolum bulunamadi.");
        }

        var announcement = new InternalAnnouncement
        {
            Title = dto.Title.Trim(),
            Content = dto.Content.Trim(),
            TargetAudience = dto.TargetAudience,
            TargetDepartmentId = dto.TargetAudience == AnnouncementAudience.Department ? dto.TargetDepartmentId : null,
            IsPinned = dto.IsPinned,
            StartsAt = dto.StartsAt ?? DateTime.UtcNow,
            ExpiresAt = dto.ExpiresAt,
            CreatedBy = createdBy,
            CreatedByName = createdByName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.InternalAnnouncements.Add(announcement);
        await _context.SaveChangesAsync(ct);

        await _context.Entry(announcement).Reference(a => a.TargetDepartment).LoadAsync(ct);
        return MapToDetail(announcement);
    }

    public async Task<AnnouncementDetailDto?> UpdateAsync(int id, UpdateAnnouncementDto dto, CancellationToken ct = default)
    {
        var announcement = await _context.InternalAnnouncements
            .Include(a => a.TargetDepartment)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (announcement == null)
            return null;

        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new InvalidOperationException("Duyuru basligi bos olamaz.");
        if (string.IsNullOrWhiteSpace(dto.Content))
            throw new InvalidOperationException("Duyuru icerigi bos olamaz.");
        if (dto.TargetAudience == AnnouncementAudience.Department && !dto.TargetDepartmentId.HasValue)
            throw new InvalidOperationException("Bolum hedefi secilmeli.");

        announcement.Title = dto.Title.Trim();
        announcement.Content = dto.Content.Trim();
        announcement.TargetAudience = dto.TargetAudience;
        announcement.TargetDepartmentId = dto.TargetAudience == AnnouncementAudience.Department ? dto.TargetDepartmentId : null;
        announcement.IsPinned = dto.IsPinned;
        announcement.StartsAt = dto.StartsAt;
        announcement.ExpiresAt = dto.ExpiresAt;
        announcement.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        await _context.Entry(announcement).Reference(a => a.TargetDepartment).LoadAsync(ct);
        return MapToDetail(announcement);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var announcement = await _context.InternalAnnouncements.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (announcement == null) return false;

        announcement.IsActive = false;
        announcement.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private static AnnouncementListDto MapToList(InternalAnnouncement a)
    {
        var preview = a.Content.Length > 200 ? a.Content.Substring(0, 200) + "..." : a.Content;
        return new AnnouncementListDto(
            a.Id,
            a.Title,
            preview,
            a.TargetAudience,
            a.TargetDepartmentId,
            a.TargetDepartment?.Name,
            a.IsPinned,
            a.StartsAt,
            a.ExpiresAt,
            a.CreatedByName,
            a.CreatedAt,
            a.IsActive);
    }

    private static AnnouncementDetailDto MapToDetail(InternalAnnouncement a)
    {
        return new AnnouncementDetailDto(
            a.Id,
            a.Title,
            a.Content,
            a.TargetAudience,
            a.TargetDepartmentId,
            a.TargetDepartment?.Name,
            a.IsPinned,
            a.StartsAt,
            a.ExpiresAt,
            a.CreatedByName,
            a.CreatedAt,
            a.UpdatedAt,
            a.IsActive);
    }
}
