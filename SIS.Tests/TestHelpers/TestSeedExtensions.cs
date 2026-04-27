using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.TestHelpers;

/// <summary>
/// Faz B B3b: In-memory test'lerde DB-driven lookup tablolarının seed'i.
/// Production'da <see cref="SIS.Infrastructure.Persistence.Seeds.ElectiveSubtypeSeed"/>
/// idempotent olarak çalışır; test'lerde Id'ler enum integer'larıyla eşlenir
/// (1=UniversityElective, 2=NonDepartmentElective, 3=TechnicalElective) çünkü
/// migration backfill bu eşlemeyi varsayar.
/// </summary>
internal static class TestSeedExtensions
{
    public static void SeedElectiveSubtypeDefaults(this AppDbContext context)
    {
        if (context.ElectiveSubtypes.Any()) return;

        context.ElectiveSubtypes.AddRange(
            new ElectiveSubtypeEntity
            {
                Id = (int)ElectiveSubtype.UniversityElective,
                Code = "UniversityElective", Name = "Üniversite Seçmeli",
                RequiresFaculty = false, ForbidsFaculty = true,
                RequiresDepartment = false, ForbidsDepartment = true,
                IsActive = true, SortOrder = 1, CreatedAt = DateTime.UtcNow,
            },
            new ElectiveSubtypeEntity
            {
                Id = (int)ElectiveSubtype.NonDepartmentElective,
                Code = "NonDepartmentElective", Name = "Bölüm Dışı Seçmeli",
                RequiresFaculty = false, ForbidsFaculty = false,
                RequiresDepartment = false, ForbidsDepartment = false,
                IsActive = true, SortOrder = 2, CreatedAt = DateTime.UtcNow,
            },
            new ElectiveSubtypeEntity
            {
                Id = (int)ElectiveSubtype.TechnicalElective,
                Code = "TechnicalElective", Name = "Teknik Seçmeli",
                RequiresFaculty = true, ForbidsFaculty = false,
                RequiresDepartment = false, ForbidsDepartment = false,
                IsActive = true, SortOrder = 3, CreatedAt = DateTime.UtcNow,
            });

        context.SaveChanges();
    }
}
