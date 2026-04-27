using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/elective-groups")]
[Authorize]
public class ElectiveGroupController(AppDbContext context) : ControllerBase
{
    // ── DTOs ────────────────────────────────────────────────────────
    public record ElectiveGroupListDto(
        int Id, string Name, string Code, ElectiveSubtype ElectiveSubtype,
        int? FacultyId, string? FacultyName,
        int? DepartmentId, string? DepartmentName,
        string? Description,
        bool IsActive, int CourseCount, DateTime CreatedAt);

    public record ElectiveGroupDetailDto(
        int Id, string Name, string Code, ElectiveSubtype ElectiveSubtype,
        int? FacultyId, string? FacultyName,
        int? DepartmentId, string? DepartmentName,
        string? Description,
        bool IsActive, DateTime CreatedAt,
        List<ElectiveGroupCourseDto> Courses);

    public record ElectiveGroupCourseDto(
        int CourseId, string CourseCode, string CourseName, int Ects);

    public record CreateElectiveGroupRequest(
        string Name, string Code, ElectiveSubtype ElectiveSubtype,
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

    public record AddCoursesToGroupRequest(List<int> CourseIds);

    // ── GET /api/elective-groups ────────────────────────────────────
    [HttpGet]
    [RequirePermission("menu.secmeli-ders-gruplari.view")]
    public async Task<IActionResult> List(
        [FromQuery] ElectiveSubtype? subtype,
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

        if (subtype.HasValue)
            query = query.Where(eg => eg.ElectiveSubtype == subtype.Value);
        if (facultyId.HasValue)
            query = query.Where(eg => eg.FacultyId == facultyId.Value);
        if (departmentId.HasValue)
            query = query.Where(eg => eg.DepartmentId == departmentId.Value);
        if (isActive.HasValue)
            query = query.Where(eg => eg.IsActive == isActive.Value);

        var list = await query
            .OrderBy(eg => eg.Code)
            .Select(eg => new ElectiveGroupListDto(
                eg.Id, eg.Name, eg.Code, eg.ElectiveSubtype,
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

        var dto = new ElectiveGroupDetailDto(
            group.Id, group.Name, group.Code, group.ElectiveSubtype,
            group.FacultyId, group.Faculty?.Name,
            group.DepartmentId, group.Department?.Name,
            group.Description,
            group.IsActive, group.CreatedAt,
            group.Courses
                .OrderBy(c => c.Course.Code)
                .Select(c => new ElectiveGroupCourseDto(
                    c.CourseId, c.Course.Code, c.Course.Name, c.Course.Ects))
                .ToList());

        return Ok(dto);
    }

    // ── POST /api/elective-groups ───────────────────────────────────
    [HttpPost]
    [RequirePermission("menu.secmeli-ders-gruplari.create")]
    public async Task<IActionResult> Create([FromBody] CreateElectiveGroupRequest req, CancellationToken ct)
    {
        var scopeError = ValidateScopeAndSubtype(req.ElectiveSubtype, req.FacultyId, req.DepartmentId);
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
            ElectiveSubtype = req.ElectiveSubtype,
            FacultyId = req.FacultyId,
            DepartmentId = req.DepartmentId,
            Description = req.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.ElectiveGroups.Add(entity);
        await context.SaveChangesAsync(ct);

        return Ok(new ElectiveGroupListDto(
            entity.Id, entity.Name, entity.Code, entity.ElectiveSubtype,
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

        // OC için bölüm de yasak
        // Subtype değişmez (Karar #14: sadece görünen ad değişebilir, kod sabit) — bu yüzden mevcut group.ElectiveSubtype okunuyor.
        // NOT: "Teknik Seçmeli için fakülte zorunlu" ve "Üniversite Seçmeli için fakülte belirtilmemeli"
        // kuralları bilinçli olarak Update'te uygulanmıyor — bir TE grubu fakülte temizlenince
        // school-wide hale gelebiliyor (bkz. Update_ClearFaculty_and_ClearDepartment_makes_group_school_wide).
        // Bu yüzden ValidateScopeAndSubtype helper'ı yerine sadece iki kuralı inline tutuyoruz.
        if (group.ElectiveSubtype == ElectiveSubtype.UniversityElective && newDepartmentId.HasValue)
            return BadRequest(new { message = "Üniversite Seçmeli için bölüm belirtilmemelidir." });

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
    /// Scope (fakülte/bölüm) ve alt-tip kombinasyonunu doğrular. Geçersizse
    /// uygun BadRequest döner; geçerliyse null. Create ve Update aynı kuralları
    /// paylaşır — bu helper iki yolda da DRY uygulanabilir.
    /// </summary>
    private static IActionResult? ValidateScopeAndSubtype(
        ElectiveSubtype subtype,
        int? facultyId,
        int? departmentId)
    {
        if (subtype == ElectiveSubtype.TechnicalElective && !facultyId.HasValue)
            return new BadRequestObjectResult(new { message = "Teknik Seçmeli için fakülte zorunludur." });
        if (subtype == ElectiveSubtype.UniversityElective && facultyId.HasValue)
            return new BadRequestObjectResult(new { message = "Üniversite Seçmeli için fakülte belirtilmemelidir." });
        if (departmentId.HasValue && !facultyId.HasValue)
            return new BadRequestObjectResult(new { message = "Bölüm seçildiğinde fakülte de zorunludur." });
        if (subtype == ElectiveSubtype.UniversityElective && departmentId.HasValue)
            return new BadRequestObjectResult(new { message = "Üniversite Seçmeli için bölüm belirtilmemelidir." });
        return null;
    }
}
