using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence.Seeds;

/// <summary>
/// Seeds default Ders Kategorisi values. Admin can extend in future.
/// </summary>
public static class CourseCategorySeed
{
    public static readonly IReadOnlyList<CourseCategory> All = new List<CourseCategory>
    {
        new() { Code = "Default",       Name = "Varsayılan",   SortOrder = 10, IsActive = true },
        new() { Code = "Compulsory",    Name = "Zorunlu",      SortOrder = 20, IsActive = true },
        new() { Code = "Elective",      Name = "Seçmeli",      SortOrder = 30, IsActive = true },
        new() { Code = "Common",        Name = "Ortak",        SortOrder = 40, IsActive = true },
        new() { Code = "ServiceCourse", Name = "Servis Dersi", SortOrder = 50, IsActive = true },
    };

    public static async Task SeedAsync(AppDbContext context)
    {
        var existing = await context.CourseCategories
            .Select(c => c.Code)
            .ToListAsync();
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toInsert = All.Where(c => !existingSet.Contains(c.Code)).ToList();
        if (toInsert.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var c in toInsert) c.CreatedAt = now;

        context.CourseCategories.AddRange(toInsert);
        await context.SaveChangesAsync();
    }
}
