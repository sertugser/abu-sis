using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence.Seeds;

/// <summary>
/// Faz B B3a — DB-driven seçmeli ders tipleri seed'i.
/// Idempotent merge: <see cref="Defaults"/> içindeki Code'su tabloda olmayanları ekler,
/// mevcut kayıtlara dokunmaz (admin'in Name/SortOrder gibi değişikliklerini koruma garantisi).
/// </summary>
public static class ElectiveSubtypeSeed
{
    public static readonly ElectiveSubtypeEntity[] Defaults =
    {
        new()
        {
            Code = "UniversityElective", Name = "Üniversite Seçmeli",
            RequiresFaculty = false, ForbidsFaculty = true,
            RequiresDepartment = false, ForbidsDepartment = true,
            IsActive = true, SortOrder = 1,
        },
        new()
        {
            Code = "NonDepartmentElective", Name = "Bölüm Dışı Seçmeli",
            RequiresFaculty = false, ForbidsFaculty = false,
            RequiresDepartment = false, ForbidsDepartment = false,
            IsActive = true, SortOrder = 2,
        },
        new()
        {
            Code = "TechnicalElective", Name = "Teknik Seçmeli",
            RequiresFaculty = true, ForbidsFaculty = false,
            RequiresDepartment = false, ForbidsDepartment = false,
            IsActive = true, SortOrder = 3,
        },
    };

    /// <summary>
    /// Idempotent merge — sadece Code'su tabloda olmayanları ekler.
    /// </summary>
    public static async Task SeedAsync(AppDbContext context, CancellationToken ct = default)
    {
        var existingCodes = await context.ElectiveSubtypes
            .Select(s => s.Code)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existingCodes, StringComparer.Ordinal);

        var toAdd = Defaults
            .Where(d => !existingSet.Contains(d.Code))
            .Select(d => new ElectiveSubtypeEntity
            {
                Code = d.Code,
                Name = d.Name,
                RequiresFaculty = d.RequiresFaculty,
                ForbidsFaculty = d.ForbidsFaculty,
                RequiresDepartment = d.RequiresDepartment,
                ForbidsDepartment = d.ForbidsDepartment,
                IsActive = d.IsActive,
                SortOrder = d.SortOrder,
                CreatedAt = DateTime.UtcNow,
            })
            .ToList();

        if (toAdd.Count == 0) return;

        context.ElectiveSubtypes.AddRange(toAdd);
        await context.SaveChangesAsync(ct);
    }
}
