using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Koltuk (EnrolledCount) muhasebesinin TEK kaynağı. Daha önce aynı mantık Submit/PlanAdd/
/// DropWithdraw/Approval ve cleanup job'larında ayrı ayrı kopyalanmıştı (ikibaşlılık + sızıntı
/// riski). Tüm rezervasyon/bırakma buradan geçer.
///
/// Çağıran taraf transaction'ı (Serializable) ve SaveChanges'i sahiplenir; bu helper yalnızca
/// takip edilen (tracked) CourseSection üzerinde sayımı değiştirir. Eş zamanlılık koruması için
/// CourseSection.RowVersion EF concurrency token'ı SaveChanges'te otomatik devreye girer.
/// </summary>
internal static class SeatLedger
{
    /// <summary>
    /// Cross-list grubundaki section'lar kapasiteyi paylaşır (Madde 14). Etkin dolu sayısı:
    /// grup varsa grup toplamı, yoksa section'ın kendi sayısı.
    /// </summary>
    public static async Task<int> GetEffectiveEnrolledAsync(
        AppDbContext db, CourseSection section, CancellationToken ct = default)
    {
        if (!section.CrossListGroupId.HasValue)
            return section.EnrolledCount;

        return await db.CourseSections
            .Where(cs => cs.CrossListGroupId == section.CrossListGroupId.Value)
            .SumAsync(cs => cs.EnrolledCount, ct);
    }

    /// <summary>
    /// Taze kapasite kontrolü + koltuk rezervasyonu. Transaction içinde çağrılmalıdır.
    /// Dolu ise false döner (sayım değişmez); aksi halde EnrolledCount artırılır ve true döner.
    /// </summary>
    public static async Task<bool> TryReserveAsync(
        AppDbContext db, CourseSection section, CancellationToken ct = default)
    {
        // Eş zamanlı başka bir kaydın etkisini görmek için taze oku.
        await db.Entry(section).ReloadAsync(ct);

        var effective = await GetEffectiveEnrolledAsync(db, section, ct);
        if (effective >= section.Capacity)
            return false;

        section.EnrolledCount++;
        return true;
    }

    /// <summary>Koltuğu serbest bırak (floor-0 korumalı — negatif olamaz).</summary>
    public static void Release(CourseSection section)
    {
        if (section.EnrolledCount > 0)
            section.EnrolledCount--;
    }

    /// <summary>
    /// Eş zamanlı kapasite çakışması mı? (RowVersion optimistic concurrency başarısızlığı veya
    /// PostgreSQL Serializable serialization_failure/deadlock). Bu durumlar kullanıcıya 500 değil
    /// "kontenjan bu esnada değişti, tekrar deneyin" olarak yansıtılmalı.
    /// </summary>
    public static bool IsConcurrencyConflict(Exception ex)
    {
        if (ex is DbUpdateConcurrencyException)
            return true;

        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is Npgsql.PostgresException pg
                && pg.SqlState is "40001" or "40P01") // serialization_failure / deadlock_detected
                return true;
        }

        return false;
    }
}
