using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    public static async Task SeedAnnouncementCategoriesAsync(AppDbContext context)
    {
        // Sistem kategorileri — silinemez, sadece görsel özellikleri admin tarafından düzenlenebilir.
        // Renkler Tailwind palette'inden alındı (oklch değil HEX, frontend de aynı kodu kullanır).
        var defaults = new List<AnnouncementCategory>
        {
            new() { Name = "Akademik", ColorHex = "#3b82f6", IconName = "GraduationCap", SortOrder = 10, IsSystem = true, CreatedAt = DateTime.UtcNow },
            new() { Name = "İdari",    ColorHex = "#64748b", IconName = "Building2",     SortOrder = 20, IsSystem = true, CreatedAt = DateTime.UtcNow },
            new() { Name = "Sosyal",   ColorHex = "#ec4899", IconName = "Sparkles",      SortOrder = 30, IsSystem = true, CreatedAt = DateTime.UtcNow },
            new() { Name = "Ders",     ColorHex = "#10b981", IconName = "BookOpen",      SortOrder = 40, IsSystem = true, CreatedAt = DateTime.UtcNow },
        };

        var existingNames = await context.AnnouncementCategories
            .AsNoTracking()
            .Select(c => c.Name)
            .ToListAsync();

        var existingSet = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = defaults
            .Where(d => !existingSet.Contains(d.Name))
            .ToList();

        if (missing.Count == 0) return;

        context.AnnouncementCategories.AddRange(missing);
        await context.SaveChangesAsync();
    }
}
