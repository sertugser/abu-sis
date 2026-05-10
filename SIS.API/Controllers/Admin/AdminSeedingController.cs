using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers.Admin;

/// <summary>
/// Geliştirici/test seeding endpoint'leri. Sadece SystemAdministrator erişebilir.
/// Auto-schedule algoritma testleri için Mühendislik fakültesinin gerçek dönem verisini
/// (Güz 2024-25 + Bahar 2025-26) DB'ye yükler. Tüm kayıtlar admin UI'dan düzenlenebilir.
///
/// Hardcoded değil: bölüm eşleştirmesi ve oda bilgileri ilk yüklemede varsayılan değerler;
/// sonradan admin tarafından Course/Department/Classroom yönetim sayfalarından değiştirilebilir.
/// </summary>
[ApiController]
[Authorize(Roles = "SystemAdministrator")]
[Route("api/admin/seed")]
public class AdminSeedingController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminSeedingController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Auto-schedule test verisini DB'ye yükler. Idempotent — eksikleri tamamlar.
    /// Yan etki: H2/H-WL/H-DL/H-DO/H15/S2/S8 hoca kısıtlarını pasife alır
    /// (test verisinde hoca atanmadığı için anlamsız olurdu).
    /// </summary>
    [HttpPost("auto-schedule-test-data")]
    public async Task<ActionResult<AutoScheduleTestDataStatus>> SeedAutoScheduleTestData()
    {
        var status = await SeedData.SeedAutoScheduleTestDataAsync(_context);
        return Ok(status);
    }

    /// <summary>
    /// Test verisini geri alır: Güz 2024-25 ve Bahar 2025-26'daki test course'larına bağlı
    /// section'ları (ve schedule'larını) siler, hoca kısıtlarını geri aktif eder.
    /// Course ve Classroom kayıtları KALIR (admin UI'dan elle silinebilir).
    /// </summary>
    [HttpDelete("auto-schedule-test-data")]
    public async Task<ActionResult<object>> RemoveAutoScheduleTestData()
    {
        var sectionsRemoved = await SeedData.RemoveAutoScheduleTestDataAsync(_context);
        return Ok(new { sectionsRemoved });
    }

    /// <summary>
    /// Test verisinin mevcut durumunu raporlar: kaç section var, kaç ders var,
    /// hoca kısıtları pasif mi.
    /// </summary>
    [HttpGet("auto-schedule-test-data/status")]
    public async Task<ActionResult<AutoScheduleTestDataStatus>> GetAutoScheduleTestDataStatus()
    {
        var status = await SeedData.GetAutoScheduleTestDataStatusAsync(_context);
        return Ok(status);
    }
}
