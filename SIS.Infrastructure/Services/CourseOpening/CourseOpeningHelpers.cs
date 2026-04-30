using Microsoft.EntityFrameworkCore;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.CourseOpening;

/// <summary>
/// Birden fazla CourseOpening servisinin paylaştığı yardımcı sorgular.
/// Faz 3.C: Eski monolitik CourseOpeningService'in private helper'ları buraya taşındı.
/// </summary>
public sealed class CourseOpeningHelpers(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    /// <summary>Verilen ID'ler için "Title FirstName LastName" sözlüğü döner.</summary>
    public async Task<Dictionary<int, string>> LoadInstructorNameMapAsync(IEnumerable<int> instructorIds, CancellationToken ct)
    {
        var ids = instructorIds.Distinct().ToList();
        if (ids.Count == 0) return new();

        var instructors = await _context.Instructors
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .Select(i => new { i.Id, i.Title, i.UserId })
            .ToListAsync(ct);

        var userIds = instructors.Select(i => i.UserId).Distinct().ToList();
        var userMap = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.FirstName, u.LastName }, ct);

        return instructors.ToDictionary(
            i => i.Id,
            i => userMap.TryGetValue(i.UserId, out var u) ? $"{i.Title} {u.FirstName} {u.LastName}" : $"{i.Title} (#{i.Id})");
    }

    /// <summary>Hocanın ders sahibi bölümün fakültesinde olup olmadığını doğrular.</summary>
    public async Task ValidateInstructorAssignmentAsync(int instructorId, int? owningDepartmentId, CancellationToken ct)
    {
        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.Id == instructorId, ct);

        if (instructor == null)
            throw new InvalidOperationException("Atanan öğretim üyesi bulunamadı.");

        if (owningDepartmentId.HasValue)
        {
            var courseFacultyId = await _context.Departments
                .AsNoTracking()
                .Where(d => d.Id == owningDepartmentId.Value)
                .Select(d => d.FacultyId)
                .FirstOrDefaultAsync(ct);

            if (instructor.Department?.FacultyId != courseFacultyId)
                throw new InvalidOperationException("Öğretim üyesi bu fakülteye ait değil.");
        }
    }

    /// <summary>Aktif/Beklemede kayıtlar (Dropped ve Draft hariç).</summary>
    public Task<int> GetRegisteredCountAsync(int sectionId, CancellationToken ct)
    {
        return _context.CourseRegistrations.CountAsync(
            r => r.CourseSectionId == sectionId
                && r.Status != RegistrationStatus.Dropped
                && r.Status != RegistrationStatus.Draft,
            ct);
    }

    /// <summary>Sadece Draft veya PendingAdvisorApproval olan kayıtlar.</summary>
    public Task<int> GetDraftOrPendingCountAsync(int sectionId, CancellationToken ct)
    {
        return _context.CourseRegistrations.CountAsync(
            r => r.CourseSectionId == sectionId
                && (r.Status == RegistrationStatus.Draft || r.Status == RegistrationStatus.PendingAdvisorApproval),
            ct);
    }
}
