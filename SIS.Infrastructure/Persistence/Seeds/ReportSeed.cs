using Microsoft.EntityFrameworkCore;
using SIS.Domain.Identity;

namespace SIS.Infrastructure.Persistence.Seeds;

/// <summary>
/// Handler'ı olan raporları DB'ye idempotent ekler. Code = handler.Code ile birebir.
/// Kategori 'genel-raporlar' altında.
/// </summary>
public static class ReportSeed
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var category = await db.ReportCategories.FirstOrDefaultAsync(c => c.Code == "genel-raporlar", ct);
        if (category is null)
        {
            category = new ReportCategory { Code = "genel-raporlar", Name = "Genel Raporlar" };
            db.ReportCategories.Add(category);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.ReportDefinitions.AnyAsync(r => r.Code == "ogrenci-listesi", ct))
        {
            db.ReportDefinitions.Add(new ReportDefinition
            {
                Code = "ogrenci-listesi",
                Name = "Öğrenci Listesi",
                Description = "Duruma, programa ve GNO aralığına göre öğrenci listesi.",
                CategoryId = category.Id,
            });
            await db.SaveChangesAsync(ct);
        }
    }
}
