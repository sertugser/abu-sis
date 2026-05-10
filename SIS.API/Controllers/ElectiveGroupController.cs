using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/elective-groups")]
[Authorize]
public class ElectiveGroupController(AppDbContext context) : ControllerBase
{
    // ── DTOs ────────────────────────────────────────────────────────
    // Faz E: Deprecated ElectiveSubtype enum DTO field'ı kaldırıldı.
    // Sadece ElectiveSubtypeId FK döner; frontend display useElectiveSubtypeList lookup ile.
    public record ElectiveGroupListDto(
        int Id, string Name, string Code,
        int ElectiveSubtypeId,
        int? FacultyId, string? FacultyName,
        int? DepartmentId, string? DepartmentName,
        string? Description,
        bool IsActive, int CourseCount, DateTime CreatedAt);

    public record ElectiveGroupDetailDto(
        int Id, string Name, string Code,
        int ElectiveSubtypeId,
        int? FacultyId, string? FacultyName,
        int? DepartmentId, string? DepartmentName,
        string? Description,
        bool IsActive, DateTime CreatedAt,
        List<ElectiveGroupCourseDto> Courses,
        List<ElectiveGroupUsageDto> Usages);

    public record ElectiveGroupCourseDto(
        int CourseId, string CourseCode, string CourseName, int Ects);

    /** Bu seçmeli grubun bağlı olduğu müfredat slotları —
     * "Bu havuz nerede kullanılıyor?" sorusuna cevap. */
    public record ElectiveGroupUsageDto(
        int PoolId,
        int CurriculumId, string CurriculumName, string CurriculumVersion,
        int ProgramId, string ProgramName,
        string SlotName,
        int SuggestedSemesterStart, int SuggestedSemesterEnd,
        int RequiredCourseCount, int RequiredEcts,
        int? EctsPerCourse);

    public record CreateElectiveGroupRequest(
        string Name, string Code,
        int ElectiveSubtypeId,
        int? FacultyId = null,
        int? DepartmentId = null,
        string? Description = null);

    public record UpdateElectiveGroupRequest(
        string? Name = null, string? Code = null,
        int? FacultyId = null,
        int? DepartmentId = null,
        bool ClearFaculty = false,
        bool ClearDepartment = false,
        string? Description = null,
        bool? IsActive = null);
    // NOT: Update DTO'da ElectiveSubtype/ElectiveSubtypeId YOK — Karar #14 gereği subtype değişmez.

    public record AddCoursesToGroupRequest(List<int> CourseIds);

    // ── GET /api/elective-groups ────────────────────────────────────
    [HttpGet]
    [RequirePermission("menu.secmeli-ders-gruplari.view")]
    public async Task<IActionResult> List(
        [FromQuery] int? subtypeId,
        [FromQuery] int? facultyId,
        [FromQuery] int? departmentId,
        [FromQuery] bool? isActive,
        CancellationToken ct)
    {
        var query = context.ElectiveGroups
            .AsNoTracking()
            .Include(eg => eg.Faculty)
            .Include(eg => eg.Department)
            .Include(eg => eg.Courses)
            .AsQueryable();

        // Faz E: sadece DB-driven subtypeId filter (deprecated subtype enum kalktı)
        if (subtypeId.HasValue)
            query = query.Where(eg => eg.ElectiveSubtypeId == subtypeId.Value);
        if (facultyId.HasValue)
            query = query.Where(eg => eg.FacultyId == facultyId.Value);
        if (departmentId.HasValue)
            query = query.Where(eg => eg.DepartmentId == departmentId.Value);
        if (isActive.HasValue)
            query = query.Where(eg => eg.IsActive == isActive.Value);

        var list = await query
            .OrderBy(eg => eg.Code)
            .Select(eg => new ElectiveGroupListDto(
                eg.Id, eg.Name, eg.Code,
                eg.ElectiveSubtypeId,
                eg.FacultyId, eg.Faculty != null ? eg.Faculty.Name : null,
                eg.DepartmentId, eg.Department != null ? eg.Department.Name : null,
                eg.Description, eg.IsActive,
                eg.Courses.Count, eg.CreatedAt))
            .ToListAsync(ct);

        return Ok(list);
    }

    // ── GET /api/elective-groups/{id} ───────────────────────────────
    [HttpGet("{id:int}")]
    [RequirePermission("menu.secmeli-ders-gruplari.view")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var group = await context.ElectiveGroups
            .AsNoTracking()
            .Include(eg => eg.Faculty)
            .Include(eg => eg.Department)
            .Include(eg => eg.Courses).ThenInclude(egc => egc.Course)
            .FirstOrDefaultAsync(eg => eg.Id == id, ct);

        if (group is null)
            return NotFound(new { message = "Seçmeli ders grubu bulunamadı." });

        // "Bu havuz nerede kullanılıyor?" — müfredata bağlı pool slotları.
        var usages = await context.CurriculumElectivePools
            .AsNoTracking()
            .Where(p => p.ElectiveGroupId == id)
            .Include(p => p.Curriculum).ThenInclude(c => c.Program)
            .OrderBy(p => p.Curriculum.Program.Name)
            .ThenBy(p => p.Curriculum.Version)
            .ThenBy(p => p.SlotName)
            .Select(p => new ElectiveGroupUsageDto(
                p.Id,
                p.CurriculumId, p.Curriculum.Name, p.Curriculum.Version,
                p.Curriculum.ProgramId, p.Curriculum.Program.Name,
                p.SlotName,
                p.SuggestedSemesterStart, p.SuggestedSemesterEnd,
                p.RequiredCourseCount, p.RequiredEcts,
                p.EctsPerCourse))
            .ToListAsync(ct);

        var dto = new ElectiveGroupDetailDto(
            group.Id, group.Name, group.Code,
            group.ElectiveSubtypeId,
            group.FacultyId, group.Faculty?.Name,
            group.DepartmentId, group.Department?.Name,
            group.Description,
            group.IsActive, group.CreatedAt,
            group.Courses
                .OrderBy(c => c.Course.Code)
                .Select(c => new ElectiveGroupCourseDto(
                    c.CourseId, c.Course.Code, c.Course.Name, c.Course.Ects))
                .ToList(),
            usages);

        return Ok(dto);
    }

    // ── POST /api/elective-groups ───────────────────────────────────
    [HttpPost]
    [RequirePermission("menu.secmeli-ders-gruplari.create")]
    public async Task<IActionResult> Create([FromBody] CreateElectiveGroupRequest req, CancellationToken ct)
    {
        // Faz E: Subtype'ı DB'den çöz (FK only — deprecated enum kalktı).
        var (subtype, subtypeError) = await ResolveSubtypeAsync(req.ElectiveSubtypeId, ct);
        if (subtypeError != null) return subtypeError;

        var scopeError = ValidateScopeAgainstSubtype(subtype!, req.FacultyId, req.DepartmentId);
        if (scopeError != null) return scopeError;

        var codeExists = await context.ElectiveGroups
            .AnyAsync(eg => eg.Code == req.Code, ct);
        if (codeExists)
            return BadRequest(new { message = $"'{req.Code}' kodu zaten mevcut." });

        if (req.FacultyId.HasValue)
        {
            var facultyExists = await context.Faculties.AnyAsync(f => f.Id == req.FacultyId.Value, ct);
            if (!facultyExists)
                return NotFound(new { message = "Fakülte bulunamadı." });
        }

        // Bölüm varlığı kontrolü — bölüm verilen fakülteye ait olmalı
        if (req.DepartmentId.HasValue)
        {
            var deptExists = await context.Departments
                .AnyAsync(d => d.Id == req.DepartmentId.Value && d.FacultyId == req.FacultyId, ct);
            if (!deptExists)
                return BadRequest(new { message = "Bölüm bulunamadı veya seçilen fakülteye ait değil." });
        }

        var entity = new Domain.Entities.ElectiveGroup
        {
            Name = req.Name,
            Code = req.Code,
            ElectiveSubtypeId = subtype!.Id,
            FacultyId = req.FacultyId,
            DepartmentId = req.DepartmentId,
            Description = req.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.ElectiveGroups.Add(entity);
        await context.SaveChangesAsync(ct);

        return Ok(new ElectiveGroupListDto(
            entity.Id, entity.Name, entity.Code,
            entity.ElectiveSubtypeId,
            entity.FacultyId, null,
            entity.DepartmentId, null,
            entity.Description,
            entity.IsActive, 0, entity.CreatedAt));
    }

    // ── PUT /api/elective-groups/{id} ───────────────────────────────
    [HttpPut("{id:int}")]
    [RequirePermission("menu.secmeli-ders-gruplari.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateElectiveGroupRequest req, CancellationToken ct)
    {
        var group = await context.ElectiveGroups
            .FirstOrDefaultAsync(eg => eg.Id == id, ct);
        if (group is null)
            return NotFound(new { message = "Seçmeli ders grubu bulunamadı." });

        if (req.Code is not null && req.Code != group.Code)
        {
            var codeExists = await context.ElectiveGroups
                .AnyAsync(eg => eg.Code == req.Code && eg.Id != id, ct);
            if (codeExists)
                return BadRequest(new { message = $"'{req.Code}' kodu zaten mevcut." });
            group.Code = req.Code;
        }

        // Guard: Fakülte temizlenirken bölüm de temizlenmelidir (dept fakülteye bağımlı).
        if (req.ClearFaculty && !req.ClearDepartment && (req.DepartmentId.HasValue || group.DepartmentId.HasValue))
            return BadRequest(new { message = "Fakülte temizlenirken bölüm de temizlenmelidir." });

        // Effective values (clear flag öncelikli; yoksa request öncelikli; yoksa mevcut değer)
        var newFacultyId = req.ClearFaculty ? null : (req.FacultyId ?? group.FacultyId);
        var newDepartmentId = req.ClearDepartment ? null : (req.DepartmentId ?? group.DepartmentId);

        // Bölüm seçilmişse fakülte de zorunlu
        if (newDepartmentId.HasValue && !newFacultyId.HasValue)
            return BadRequest(new { message = "Bölüm seçildiğinde fakülte de zorunludur." });

        // ForbidsDepartment flag'li tip için (örn. OC) bölüm yasak.
        // Subtype değişmez (Karar #14) — group'un mevcut subtype'ı DB'den okunup flag kontrol edilir.
        // NOT: RequiresFaculty ve ForbidsFaculty Update'te bilinçli olarak uygulanmıyor —
        // bir TE grubu fakülte temizlenince school-wide hale gelebiliyor
        // (bkz. Update_ClearFaculty_and_ClearDepartment_makes_group_school_wide).
        // Bu yüzden sadece "ForbidsDepartment + DepartmentId.HasValue" kuralını inline tutuyoruz.
        if (newDepartmentId.HasValue)
        {
            var groupSubtype = await context.ElectiveSubtypes
                .FirstOrDefaultAsync(s => s.Id == group.ElectiveSubtypeId, ct);
            if (groupSubtype != null && groupSubtype.ForbidsDepartment)
                return BadRequest(new { message = $"{groupSubtype.Name} için bölüm belirtilmemelidir." });
        }

        // Bölüm varlığı kontrolü — bölüm verilen fakülteye ait olmalı
        if (newDepartmentId.HasValue &&
            (newDepartmentId != group.DepartmentId || newFacultyId != group.FacultyId))
        {
            var deptExists = await context.Departments
                .AnyAsync(d => d.Id == newDepartmentId.Value && d.FacultyId == newFacultyId, ct);
            if (!deptExists)
                return BadRequest(new { message = "Bölüm bulunamadı veya seçilen fakülteye ait değil." });
        }

        // Scope değişimi varsa: mevcut müfredat bağlantısı yoksa serbest, varsa engelle (Karar #12)
        if (newDepartmentId != group.DepartmentId || newFacultyId != group.FacultyId)
        {
            var hasBindings = await context.CurriculumElectivePools
                .AnyAsync(p => p.ElectiveGroupId == id, ct);
            if (hasBindings)
                return BadRequest(new { message = "Bu grup mevcut müfredatlara bağlı. Scope (fakülte/bölüm) değiştirmeden önce tüm bağlantıları kaldırın." });
        }

        if (req.Name is not null) group.Name = req.Name;

        if (req.ClearFaculty) group.FacultyId = null;
        else if (req.FacultyId.HasValue) group.FacultyId = req.FacultyId;

        if (req.ClearDepartment) group.DepartmentId = null;
        else if (req.DepartmentId.HasValue) group.DepartmentId = req.DepartmentId;

        if (req.Description is not null) group.Description = req.Description;
        if (req.IsActive.HasValue) group.IsActive = req.IsActive.Value;

        group.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        return Ok(new { message = "Seçmeli ders grubu güncellendi." });
    }

    // ── DELETE /api/elective-groups/{id} ────────────────────────────
    [HttpDelete("{id:int}")]
    [RequirePermission("menu.secmeli-ders-gruplari.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var group = await context.ElectiveGroups
            .FirstOrDefaultAsync(eg => eg.Id == id, ct);
        if (group is null)
            return NotFound(new { message = "Seçmeli ders grubu bulunamadı." });

        group.IsActive = false;
        group.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        return Ok(new { message = "Seçmeli ders grubu pasif yapıldı." });
    }

    // ── GET /api/elective-groups/{id}/courses ───────────────────────
    [HttpGet("{id:int}/courses")]
    [RequirePermission("menu.secmeli-ders-gruplari.view")]
    public async Task<IActionResult> ListCourses(int id, CancellationToken ct)
    {
        var exists = await context.ElectiveGroups.AnyAsync(eg => eg.Id == id, ct);
        if (!exists)
            return NotFound(new { message = "Seçmeli ders grubu bulunamadı." });

        var courses = await context.ElectiveGroupCourses
            .AsNoTracking()
            .Where(egc => egc.ElectiveGroupId == id)
            .Include(egc => egc.Course)
            .OrderBy(egc => egc.Course.Code)
            .Select(egc => new ElectiveGroupCourseDto(
                egc.CourseId, egc.Course.Code, egc.Course.Name, egc.Course.Ects))
            .ToListAsync(ct);

        return Ok(courses);
    }

    // ── POST /api/elective-groups/{id}/courses ──────────────────────
    [HttpPost("{id:int}/courses")]
    [RequirePermission("menu.secmeli-ders-gruplari.add-course")]
    public async Task<IActionResult> AddCourses(int id, [FromBody] AddCoursesToGroupRequest req, CancellationToken ct)
    {
        var group = await context.ElectiveGroups
            .FirstOrDefaultAsync(eg => eg.Id == id, ct);
        if (group is null)
            return NotFound(new { message = "Seçmeli ders grubu bulunamadı." });

        var existingCourseIds = await context.ElectiveGroupCourses
            .Where(egc => egc.ElectiveGroupId == id)
            .Select(egc => egc.CourseId)
            .ToListAsync(ct);

        var newCourseIds = req.CourseIds.Except(existingCourseIds).ToList();
        if (newCourseIds.Count == 0)
            return BadRequest(new { message = "Tüm dersler zaten grupta mevcut." });

        var validCourseIds = await context.Courses
            .Where(c => newCourseIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);

        var invalidIds = newCourseIds.Except(validCourseIds).ToList();
        if (invalidIds.Count > 0)
            return BadRequest(new { message = $"Bulunamayan ders ID'leri: {string.Join(", ", invalidIds)}" });

        foreach (var courseId in validCourseIds)
        {
            context.ElectiveGroupCourses.Add(new Domain.Entities.ElectiveGroupCourse
            {
                ElectiveGroupId = id,
                CourseId = courseId,
                AddedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync(ct);

        return Ok(new { message = $"{validCourseIds.Count} ders gruba eklendi.", addedCount = validCourseIds.Count });
    }

    // ── DELETE /api/elective-groups/{id}/courses/{courseId} ──────────
    [HttpDelete("{id:int}/courses/{courseId:int}")]
    [RequirePermission("menu.secmeli-ders-gruplari.remove-course")]
    public async Task<IActionResult> RemoveCourse(int id, int courseId, CancellationToken ct)
    {
        var egc = await context.ElectiveGroupCourses
            .FindAsync([id, courseId], ct);
        if (egc is null)
            return NotFound(new { message = "Ders grupta bulunamadı." });

        context.ElectiveGroupCourses.Remove(egc);
        await context.SaveChangesAsync(ct);

        return NoContent();
    }

    // ── Helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Faz E: Subtype'ı DB'den çözümler (FK only — deprecated enum fallback kalktı).
    /// IsActive=false ise reddeder.
    /// </summary>
    private async Task<(ElectiveSubtypeEntity? Subtype, IActionResult? Error)> ResolveSubtypeAsync(
        int subtypeId,
        CancellationToken ct)
    {
        if (subtypeId <= 0)
            return (null, new BadRequestObjectResult(new { message = "Seçmeli ders tipi belirtilmedi." }));

        var subtype = await context.ElectiveSubtypes
            .FirstOrDefaultAsync(s => s.Id == subtypeId, ct);
        if (subtype == null)
            return (null, new BadRequestObjectResult(new { message = $"ID={subtypeId} olan seçmeli ders tipi bulunamadı." }));
        if (!subtype.IsActive)
            return (null, new BadRequestObjectResult(new { message = $"'{subtype.Name}' tipi pasif durumda — yeni grup açılamaz." }));

        return (subtype, null);
    }

    /// <summary>
    /// Faz B B3b: Subtype flag'lerine göre dinamik scope doğrulaması.
    /// Admin yeni tip eklediğinde (örn. RequiresFaculty=false) hardcoded if'ler
    /// değil, DB'deki bool flag'ler kullanılır.
    /// </summary>
    private static IActionResult? ValidateScopeAgainstSubtype(
        ElectiveSubtypeEntity subtype,
        int? facultyId,
        int? departmentId)
    {
        // Subtype-bağımsız invariant: bölüm seçilmişse fakülte de zorunlu.
        if (departmentId.HasValue && !facultyId.HasValue)
            return new BadRequestObjectResult(new { message = "Bölüm seçildiğinde fakülte de zorunludur." });

        if (subtype.RequiresFaculty && !facultyId.HasValue)
            return new BadRequestObjectResult(new { message = $"{subtype.Name} için fakülte zorunludur." });
        if (subtype.ForbidsFaculty && facultyId.HasValue)
            return new BadRequestObjectResult(new { message = $"{subtype.Name} için fakülte belirtilmemelidir." });
        if (subtype.RequiresDepartment && !departmentId.HasValue)
            return new BadRequestObjectResult(new { message = $"{subtype.Name} için bölüm zorunludur." });
        if (subtype.ForbidsDepartment && departmentId.HasValue)
            return new BadRequestObjectResult(new { message = $"{subtype.Name} için bölüm belirtilmemelidir." });

        return null;
    }

}
