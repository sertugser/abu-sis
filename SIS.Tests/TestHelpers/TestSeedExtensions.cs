using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.TestHelpers;

/// <summary>
/// In-memory test'lerde DB-driven lookup tablolarının seed'i.
/// Default tip ID'leri sabit (1=OC, 2=NDE, 3=TE) — migration backfill bu eşlemeyi
/// koruyor (Faz B B3a → Faz E sonrası da geçerli).
/// </summary>
internal static class TestSeedExtensions
{
    public const int UniversityElectiveId = 1;
    public const int NonDepartmentElectiveId = 2;
    public const int TechnicalElectiveId = 3;

    public static void SeedElectiveSubtypeDefaults(this AppDbContext context)
    {
        if (context.ElectiveSubtypes.Any()) return;

        context.ElectiveSubtypes.AddRange(
            new ElectiveSubtypeEntity
            {
                Id = UniversityElectiveId,
                Code = "UniversityElective", Name = "Üniversite Seçmeli",
                RequiresFaculty = false, ForbidsFaculty = true,
                RequiresDepartment = false, ForbidsDepartment = true,
                IsActive = true, SortOrder = 1, CreatedAt = DateTime.UtcNow,
            },
            new ElectiveSubtypeEntity
            {
                Id = NonDepartmentElectiveId,
                Code = "NonDepartmentElective", Name = "Bölüm Dışı Seçmeli",
                RequiresFaculty = false, ForbidsFaculty = false,
                RequiresDepartment = false, ForbidsDepartment = false,
                IsActive = true, SortOrder = 2, CreatedAt = DateTime.UtcNow,
            },
            new ElectiveSubtypeEntity
            {
                Id = TechnicalElectiveId,
                Code = "TechnicalElective", Name = "Teknik Seçmeli",
                RequiresFaculty = true, ForbidsFaculty = false,
                RequiresDepartment = false, ForbidsDepartment = false,
                IsActive = true, SortOrder = 3, CreatedAt = DateTime.UtcNow,
            });

        context.SaveChanges();
    }
}
