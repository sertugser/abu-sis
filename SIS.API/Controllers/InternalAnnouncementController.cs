using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Announcement.DTOs;
using SIS.Application.Announcement.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/internal-announcements")]
[Authorize]
public class InternalAnnouncementController : ControllerBase
{
    private readonly IInternalAnnouncementService _service;
    private readonly IAnnouncementAttachmentService _attachmentService;
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public InternalAnnouncementController(
        IInternalAnnouncementService service,
        IAnnouncementAttachmentService attachmentService,
        AppDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _service           = service;
        _attachmentService = attachmentService;
        _context           = context;
        _userManager       = userManager;
    }

    // ── Duyuru okuma (tüm roller) ─────────────────────────────���───────────────

    [HttpGet("active")]
    [RequireAnyPermission("menu.duyurular.view", "menu.duyurular-admin.view")]
    public async Task<IActionResult> GetActive([FromQuery] int? max, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var (roles, deptId, facId, sectionIds, advisorUid, studentYear) = await ResolveUserScopeAsync(userId, ct);
        var items = await _service.GetActiveForUserAsync(userId, roles, deptId, facId, sectionIds, advisorUid, studentYear, max, ct);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    [RequireAnyPermission("menu.duyurular.view", "menu.duyurular-admin.view", "menu.duyuru-yonetimi.view")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? requestingUserId = int.TryParse(userIdClaim, out var uid) ? uid : null;

        var item = await _service.GetByIdAsync(id, requestingUserId, ct);
        if (item is null)
            return NotFound(new { error = "Duyuru bulunamadı." });
        return Ok(item);
    }

    // ── Read tracking (Madde 5) ──────────────────────────────────────────────

    [HttpPost("{id:int}/read")]
    [RequirePermission("menu.duyurular.view")]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var uid)) return Unauthorized();

        var marked = await _service.MarkAsReadAsync(id, uid, ct);
        return Ok(new { marked });
    }

    [HttpPost("read-all")]
    [RequirePermission("menu.duyurular.view")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null) return Unauthorized();

        var (roles, deptId, facId, sectionIds, advisorUid, studentYear) = await ResolveUserScopeAsync(userIdClaim, ct);
        var count = await _service.MarkAllAsReadAsync(userIdClaim, roles, deptId, facId, sectionIds, advisorUid, studentYear, ct);
        return Ok(new { markedCount = count });
    }

    [HttpGet("unread-count")]
    [RequirePermission("menu.duyurular.view")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null) return Unauthorized();

        var (roles, deptId, facId, sectionIds, advisorUid, studentYear) = await ResolveUserScopeAsync(userIdClaim, ct);
        var count = await _service.GetUnreadCountAsync(userIdClaim, roles, deptId, facId, sectionIds, advisorUid, studentYear, ct);
        return Ok(new { unreadCount = count });
    }

    // ── İstatistik (Madde 9) ────────────────────────────────────────────────

    [HttpGet("{id:int}/stats")]
    [RequirePermission("menu.duyuru-yonetimi.view")]
    public async Task<IActionResult> GetStats(int id, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null) return Unauthorized();

        // Yetkilendirme: yönetici veya duyurunun yazarı
        var isManager = User.IsInRole("SystemAdministrator") || User.IsInRole("AdministrativeStaff");
        if (!isManager)
        {
            var creator = await _context.InternalAnnouncements
                .AsNoTracking()
                .Where(a => a.Id == id)
                .Select(a => a.CreatedBy)
                .FirstOrDefaultAsync(ct);
            if (creator is null) return NotFound(new { error = "Duyuru bulunamadı." });
            if (creator != userIdClaim) return Forbid();
        }

        var stats = await _service.GetStatsAsync(id, maxReaders: 20, ct);
        if (stats is null) return NotFound(new { error = "Duyuru bulunamadı." });
        return Ok(stats);
    }

    // ── Pop-up duyurular (Madde 6) ──────────────────────────────────────────

    [HttpGet("popup-pending")]
    [RequirePermission("menu.duyurular.view")]
    public async Task<IActionResult> GetPendingPopups(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null) return Unauthorized();

        var (roles, deptId, facId, sectionIds, advisorUid, studentYear) =
            await ResolveUserScopeAsync(userIdClaim, ct);

        var items = await _service.GetPendingPopupsAsync(
            userIdClaim, roles, deptId, facId, sectionIds, advisorUid, studentYear, ct);
        return Ok(items);
    }

    [HttpPost("{id:int}/acknowledge")]
    [RequirePermission("menu.duyurular.view")]
    public async Task<IActionResult> Acknowledge(int id, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var uid)) return Unauthorized();

        var acknowledged = await _service.AcknowledgeAsync(id, uid, ct);
        return Ok(new { acknowledged });
    }

    // ── Attachments (Madde 4) ────────────────────────────────────────────────

    [HttpPost("{id:int}/attachments")]
    [RequirePermission("menu.duyuru-yonetimi.edit")]
    [RequestSizeLimit(105 * 1024 * 1024)] // settings max 100 MB + payload overhead
    public async Task<IActionResult> UploadAttachment(int id, IFormFile? file, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null) return Unauthorized();

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Dosya gönderilmedi veya boş." });

        // Sahiplik kontrolü: kendi duyurusu mu, yoksa admin override mı?
        var isAdminOverride = User.IsInRole("SystemAdministrator") || User.IsInRole("AdministrativeStaff");
        var announcement = await _context.InternalAnnouncements
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new { a.CreatedBy })
            .FirstOrDefaultAsync(ct);
        if (announcement is null)
            return NotFound(new { error = "Duyuru bulunamadı." });
        if (!isAdminOverride && announcement.CreatedBy != userIdClaim)
            return Forbid();

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _attachmentService.UploadAsync(
                id, file.FileName, stream, file.Length, file.ContentType, userIdClaim, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:int}/attachments/{attachmentId:int}/download")]
    [RequireAnyPermission("menu.duyurular.view", "menu.duyurular-admin.view", "menu.duyuru-yonetimi.view")]
    public async Task<IActionResult> DownloadAttachment(int id, int attachmentId, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null) return Unauthorized();

        // Ek doğru duyuruya mı ait?
        var parentId = await _attachmentService.GetParentAnnouncementIdAsync(attachmentId, ct);
        if (parentId is null || parentId.Value != id)
            return NotFound(new { error = "Ek bulunamadı." });

        // Yetki: yönetici / duyurunun yazarı / audience'a dahil bir kullanıcı
        var isManager = User.IsInRole("SystemAdministrator")
                     || User.IsInRole("AdministrativeStaff");

        bool authorized = isManager;
        if (!authorized)
        {
            // Duyurunun yazarı kendi ekini her zaman indirebilir
            var creatorId = await _context.InternalAnnouncements
                .AsNoTracking()
                .Where(a => a.Id == id)
                .Select(a => a.CreatedBy)
                .FirstOrDefaultAsync(ct);
            if (creatorId == userIdClaim)
            {
                authorized = true;
            }
            else
            {
                var (roles, deptId, facId, sectionIds, advisorUid, studentYear) =
                    await ResolveUserScopeAsync(userIdClaim, ct);
                authorized = await _service.CanUserSeeAsync(
                    id, userIdClaim, roles, deptId, facId, sectionIds, advisorUid, studentYear, ct);
            }
        }

        if (!authorized) return Forbid();

        var download = await _attachmentService.OpenForDownloadAsync(attachmentId, ct);
        if (download is null)
            return NotFound(new { error = "Dosya sunucudan kaldırılmış." });

        // Content-Disposition: orijinal ad ile, RFC 5987 uyumlu UTF-8 encode
        var encodedName = Uri.EscapeDataString(download.FileName);
        Response.Headers.Append(
            "Content-Disposition",
            $"attachment; filename=\"{download.FileName}\"; filename*=UTF-8''{encodedName}");

        return File(download.Content, download.MimeType, enableRangeProcessing: true);
    }

    [HttpDelete("{id:int}/attachments/{attachmentId:int}")]
    [RequirePermission("menu.duyuru-yonetimi.edit")]
    public async Task<IActionResult> DeleteAttachment(int id, int attachmentId, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null) return Unauthorized();

        var parentId = await _attachmentService.GetParentAnnouncementIdAsync(attachmentId, ct);
        if (parentId is null || parentId.Value != id)
            return NotFound(new { error = "Ek bulunamadı." });

        var isAdminOverride = User.IsInRole("SystemAdministrator") || User.IsInRole("AdministrativeStaff");

        try
        {
            var success = await _attachmentService.DeleteAsync(attachmentId, userIdClaim, isAdminOverride, ct);
            return success ? NoContent() : NotFound(new { error = "Ek bulunamadı." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    /// <summary>
    /// Kullanıcının duyuru görünürlük kapsamını (rol/bölüm/fakülte/section/danışman/sınıf) tek noktada çözer.
    /// GetActive, MarkAllAsRead ve GetUnreadCount tarafından paylaşılır.
    /// </summary>
    private async Task<(List<string> Roles, int? DeptId, int? FacultyId, List<int> SectionIds, string? AdvisorUid, int? StudentYear)>
        ResolveUserScopeAsync(string userIdClaim, CancellationToken ct)
    {
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        int?    deptId      = null;
        int?    facultyId   = null;
        var     sectionIds  = new List<int>();
        string? advisorUid  = null;
        int?    studentYear = null;

        if (!int.TryParse(userIdClaim, out var uid))
            return (roles, deptId, facultyId, sectionIds, advisorUid, studentYear);

        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.UserId == uid, ct);

        if (instructor is not null)
        {
            deptId    = instructor.DepartmentId;
            facultyId = instructor.Department?.FacultyId;
            return (roles, deptId, facultyId, sectionIds, advisorUid, studentYear);
        }

        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .Include(s => s.Advisor)
            .FirstOrDefaultAsync(s => s.UserId == uid, ct);

        if (student is null)
            return (roles, deptId, facultyId, sectionIds, advisorUid, studentYear);

        var program = student.StudentPrograms.FirstOrDefault()?.Program;
        deptId = program?.DepartmentId;
        if (deptId.HasValue)
        {
            var dept = await _context.Departments
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == deptId.Value, ct);
            facultyId = dept?.FacultyId;
        }

        sectionIds = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(r => r.StudentId == student.Id)
            .Select(r => r.CourseSectionId)
            .ToListAsync(ct);

        advisorUid  = student.Advisor?.UserId.ToString();
        studentYear = (student.CompletedSemesters / 2) + 1;

        return (roles, deptId, facultyId, sectionIds, advisorUid, studentYear);
    }

    // ── Yönetim listesi ───────────────────────────────────────────────────────

    /// <summary>
    /// SysAdmin + AdministrativeStaff: tüm duyurular.
    /// Diğer roller (DeptManager, Dean, Instructor): yalnızca kendi oluşturdukları.
    /// </summary>
    [HttpGet("all")]
    [RequirePermission("menu.duyuru-yonetimi.view")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var isAdminView = User.IsInRole("SystemAdministrator")
                       || User.IsInRole("AdministrativeStaff");

        List<AnnouncementListDto> items;
        if (isAdminView)
            items = await _service.GetAllAsync(ct);
        else
            items = await _service.GetMyCreatedAsync(userId, ct);

        return Ok(items);
    }

    // ── Form için create context ──────────────────────────────────────────────

    [HttpGet("create-context")]
    [RequirePermission("menu.duyuru-yonetimi.create")]
    public async Task<IActionResult> GetCreateContext(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value);

        var context = await _service.GetCreateContextAsync(userId, roles, ct);
        return Ok(context);
    }

    // ── CRUD (duyuru oluşturma / düzenleme / silme) ───────────────────────────

    [HttpPost]
    [RequirePermission("menu.duyuru-yonetimi.create")]
    public async Task<IActionResult> Create([FromBody] CreateAnnouncementDto dto, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Unauthorized();

        var name = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrEmpty(name)) name = user.UserName ?? "Bilinmeyen";

        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        int? senderDepartmentId = null;
        int? senderFacultyId    = null;

        if (int.TryParse(userIdClaim, out var uid))
        {
            var instructor = await _context.Instructors
                .AsNoTracking()
                .Include(i => i.Department)
                .FirstOrDefaultAsync(i => i.UserId == uid, ct);

            if (instructor is not null)
            {
                senderDepartmentId = instructor.DepartmentId;
                senderFacultyId    = instructor.Department?.FacultyId;
            }
        }

        try
        {
            var created = await _service.CreateAsync(dto, userIdClaim, name, roles, senderDepartmentId, senderFacultyId, ct);
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(new { error = msg });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("menu.duyuru-yonetimi.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAnnouncementDto dto, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var isAdminOverride = User.IsInRole("SystemAdministrator") || User.IsInRole("AdministrativeStaff");

        try
        {
            var updated = await _service.UpdateAsync(id, dto, userId, isAdminOverride, ct);
            if (updated is null) return NotFound(new { error = "Duyuru bulunamadı." });
            return Ok(updated);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("menu.duyuru-yonetimi.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var isAdminOverride = User.IsInRole("SystemAdministrator") || User.IsInRole("AdministrativeStaff");

        try
        {
            var success = await _service.DeleteAsync(id, userId, isAdminOverride, ct);
            if (!success) return NotFound(new { error = "Duyuru bulunamadı." });
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [HttpDelete("{id:int}/permanent")]
    [RequirePermission("menu.duyuru-yonetimi.delete")]
    public async Task<IActionResult> PermanentDelete(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var isAdminOverride = User.IsInRole("SystemAdministrator") || User.IsInRole("AdministrativeStaff");

        try
        {
            var success = await _service.PermanentDeleteAsync(id, userId, isAdminOverride, ct);
            if (!success) return NotFound(new { error = "Duyuru bulunamadı." });
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(new { error = msg });
        }
    }

    [HttpPost("{id:int}/reactivate")]
    [RequirePermission("menu.duyuru-yonetimi.edit")]
    public async Task<IActionResult> Reactivate(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var isAdminOverride = User.IsInRole("SystemAdministrator") || User.IsInRole("AdministrativeStaff");

        try
        {
            var success = await _service.ReactivateAsync(id, userId, isAdminOverride, ct);
            if (!success) return NotFound(new { error = "Duyuru bulunamadı." });
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }
}
