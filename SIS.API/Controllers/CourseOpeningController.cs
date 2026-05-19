using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.Services;
using SIS.Application.CourseOpening.DTOs;
using SIS.Application.CourseOpening.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

public sealed record TransferDefaultsDto(int DefaultCapacityPerSection, int MaxSectionsPerCourse);

[ApiController]
[Route("api/course-opening")]
[Authorize]
public class CourseOpeningController(
    ICourseSectionLifecycleService lifecycleService,
    ICourseOpeningQueryService queryService,
    IInstructorAssignmentService instructorAssignmentService,
    ITermCourseTransferService transferService,
    AppDbContext context,
    ISystemConfigProvider systemConfig) : ControllerBase
{
    private readonly ICourseSectionLifecycleService _lifecycleService = lifecycleService;
    private readonly ICourseOpeningQueryService _queryService = queryService;
    private readonly IInstructorAssignmentService _instructorAssignmentService = instructorAssignmentService;
    private readonly ITermCourseTransferService _transferService = transferService;
    private readonly AppDbContext _context = context;
    private readonly ISystemConfigProvider _systemConfig = systemConfig;

    // ── Listeleme / Lookup ───────────────────────────────────────────────

    [HttpGet("list")]
    [RequirePermission("menu.ders-acma-islemleri.view")]
    public async Task<IActionResult> GetOpenedCourses([FromQuery] int departmentId, [FromQuery] int termId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _queryService.GetOpenedCoursesAsync(departmentId, termId, ct);
        return Ok(result);
    }

    [HttpGet("curriculum-courses")]
    [RequirePermission("menu.ders-acma-islemleri.view")]
    public async Task<IActionResult> GetCurriculumCoursesForOpening(
        [FromQuery] int departmentId,
        [FromQuery] int termId,
        [FromQuery] bool showAll = false,
        [FromQuery] int? curriculumId = null,
        [FromQuery] bool includeUnmapped = false,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        // curriculumId verilmişse müfredatın bölüm sahipliğini doğrula (yetki kaçağı önlemi)
        if (curriculumId.HasValue)
        {
            var curriculumDeptId = await _context.Curricula
                .AsNoTracking()
                .Where(c => c.Id == curriculumId.Value)
                .Select(c => (int?)c.Program.DepartmentId)
                .FirstOrDefaultAsync(ct);
            if (curriculumDeptId != departmentId) return Forbid();
        }

        var result = await _queryService.GetCurriculumCoursesForOpeningAsync(departmentId, termId, showAll, curriculumId, includeUnmapped, ct);
        return Ok(result);
    }

    [HttpGet("instructors")]
    [RequireAnyPermission(
        "menu.ders-acma-islemleri.view",
        "menu.ogretim-elemani-atama.view",
        "menu.ders-veren-ogretim-uyeleri.view",
        "menu.toplu-danisman-atama.view")]
    public async Task<IActionResult> GetDepartmentInstructors([FromQuery] int? departmentId = null, [FromQuery] int? termId = null, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToList();
        IReadOnlyCollection<int> scopedDepartmentIds;

        if (departmentId.HasValue)
        {
            if (!await HasDepartmentAccessAsync(userId, departmentId.Value, ct)) return Forbid();
            scopedDepartmentIds = [departmentId.Value];
        }
        else
        {
            scopedDepartmentIds = await ResolveAccessibleDepartmentIdsAsync(userId, roles, ct);
            if (scopedDepartmentIds.Count == 0) return Ok(Array.Empty<InstructorOptionDto>());
        }

        var result = await _queryService.GetDepartmentInstructorsAsync(scopedDepartmentIds, termId, ct);
        return Ok(result);
    }

    [HttpGet("pool-slots")]
    [RequirePermission("menu.ders-acma-islemleri.view")]
    public async Task<IActionResult> GetPoolSlots(
        [FromQuery] int curriculumId,
        [FromQuery] int termId,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        // Müfredat hangi bölüme ait — yetki kontrolü
        var curriculumDeptId = await _context.Curricula
            .AsNoTracking()
            .Where(c => c.Id == curriculumId)
            .Select(c => (int?)c.Program.DepartmentId)
            .FirstOrDefaultAsync(ct);
        if (curriculumDeptId == null) return NotFound();
        if (!await HasDepartmentAccessAsync(userId, curriculumDeptId.Value, ct)) return Forbid();

        var result = await _queryService.GetPoolSlotsForOpeningAsync(curriculumId, termId, ct);
        return Ok(result);
    }

    [HttpGet("pool-candidates")]
    [RequirePermission("menu.ders-acma-islemleri.view")]
    public async Task<IActionResult> GetPoolCandidates(
        [FromQuery] int poolId,
        [FromQuery] int termId,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        // Pool hangi müfredata, müfredat hangi bölüme — yetki kontrolü
        var poolDeptId = await _context.CurriculumElectivePools
            .AsNoTracking()
            .Where(p => p.Id == poolId)
            .Select(p => (int?)p.Curriculum.Program.DepartmentId)
            .FirstOrDefaultAsync(ct);
        if (poolDeptId == null) return NotFound();
        if (!await HasDepartmentAccessAsync(userId, poolDeptId.Value, ct)) return Forbid();

        var result = await _queryService.GetPoolCandidatesAsync(poolId, termId, ct);
        return Ok(result);
    }

    [HttpGet("external-courses")]
    [RequirePermission("menu.ders-acma-islemleri.view")]
    public async Task<IActionResult> GetExternalCourses([FromQuery] int departmentId, [FromQuery] int termId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _queryService.GetExternalCoursesAsync(departmentId, termId, ct);
        return Ok(result);
    }

    // ── Section Yaşam Döngüsü ────────────────────────────────────────────

    [HttpPost("open")]
    [RequirePermission("menu.ders-acma-islemleri.open")]
    public async Task<IActionResult> OpenCourse([FromQuery] int departmentId, [FromQuery] int termId, [FromBody] OpenCourseRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        try
        {
            var result = await _lifecycleService.OpenCourseAsync(departmentId, termId, request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("bulk-open")]
    [RequirePermission("menu.ders-acma-islemleri.bulk-open")]
    public async Task<IActionResult> BulkOpenCourses([FromQuery] int departmentId, [FromQuery] int termId, [FromBody] BulkOpenCoursesRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _lifecycleService.BulkOpenCoursesAsync(departmentId, termId, request, ct);
        return Ok(result);
    }

    [HttpPut("section")]
    [RequirePermission("menu.ders-acma-islemleri.edit")]
    public async Task<IActionResult> UpdateSection([FromBody] UpdateOpenedSectionRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, ct);

        if (section == null) return NotFound(new { error = "Şube bulunamadı." });
        if (section.Course.DepartmentId == null) return BadRequest(new { error = "Ders bir bölüme ait değil." });
        if (!await HasDepartmentAccessAsync(userId, section.Course.DepartmentId.Value, ct)) return Forbid();

        try
        {
            var result = await _lifecycleService.UpdateSectionAsync(request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("section/{id}")]
    [RequirePermission("menu.ders-acma-islemleri.close-section")]
    public async Task<IActionResult> CloseSection(int id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == id, ct);

        if (section == null) return NotFound(new { error = "Şube bulunamadı." });
        if (section.Course.DepartmentId == null) return BadRequest(new { error = "Ders bir bölüme ait değil." });
        if (!await HasDepartmentAccessAsync(userId, section.Course.DepartmentId.Value, ct)) return Forbid();

        try
        {
            await _lifecycleService.CloseSectionAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("course/{courseId}")]
    [RequirePermission("menu.ders-acma-islemleri.close-all")]
    public async Task<IActionResult> CloseAllSections(int courseId, [FromQuery] int termId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId, ct);
        if (course == null) return NotFound(new { error = "Ders bulunamadı." });
        if (course.DepartmentId == null) return BadRequest(new { error = "Ders bir bölüme ait değil." });
        if (!await HasDepartmentAccessAsync(userId, course.DepartmentId.Value, ct)) return Forbid();

        try
        {
            var result = await _lifecycleService.CloseAllSectionsAsync(courseId, termId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Hard-delete: Dersin tüm şubelerini DB'den fiziksel siler (geri alınamaz).
    /// Aktif/Tamamlanmış kayıt, değerlendirme veya yoklama varsa reddedilir.
    /// </summary>
    [HttpDelete("course/{courseId}/hard")]
    [RequirePermission("menu.ders-acma-islemleri.close-all")]
    public async Task<IActionResult> DeleteAllSectionsHard(int courseId, [FromQuery] int termId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId, ct);
        if (course == null) return NotFound(new { error = "Ders bulunamadı." });
        if (course.DepartmentId == null) return BadRequest(new { error = "Ders bir bölüme ait değil." });
        if (!await HasDepartmentAccessAsync(userId, course.DepartmentId.Value, ct)) return Forbid();

        try
        {
            var result = await _lifecycleService.DeleteAllSectionsHardAsync(courseId, termId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("auto-split")]
    [RequirePermission("menu.subelendirme-islemleri.auto-split")]
    public async Task<IActionResult> AutoSplitCourse(
        [FromBody] AutoSplitCourseRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, ct);

        if (course == null) return NotFound(new { error = "Ders bulunamadı." });
        if (course.DepartmentId == null) return BadRequest(new { error = "Ders bir bölüme ait değil." });
        if (!await HasDepartmentAccessAsync(userId, course.DepartmentId.Value, ct)) return Forbid();

        try
        {
            var result = await _lifecycleService.AutoSplitCourseAsync(request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("transfer-students")]
    [RequirePermission("menu.subelendirme-islemleri.transfer")]
    public async Task<IActionResult> TransferStudentsBetweenSections(
        [FromBody] TransferStudentsToSectionRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var sourceSection = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == request.SourceSectionId, ct);

        if (sourceSection == null) return NotFound(new { error = "Kaynak şube bulunamadı." });
        if (sourceSection.Course.DepartmentId == null) return BadRequest(new { error = "Ders bir bölüme ait değil." });
        if (!await HasDepartmentAccessAsync(userId, sourceSection.Course.DepartmentId.Value, ct)) return Forbid();

        try
        {
            var result = await _lifecycleService.TransferStudentsBetweenSectionsAsync(request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("section/{id}/reactivate")]
    [RequirePermission("menu.ders-acma-islemleri.reactivate")]
    public async Task<IActionResult> ReactivateSection(int id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == id, ct);

        if (section == null) return NotFound(new { error = "Şube bulunamadı." });
        if (section.Course.DepartmentId == null) return BadRequest(new { error = "Ders bir bölüme ait değil." });
        if (!await HasDepartmentAccessAsync(userId, section.Course.DepartmentId.Value, ct)) return Forbid();

        try
        {
            await _lifecycleService.ReactivateSectionAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Hoca Atama ───────────────────────────────────────────────────────

    [HttpPost("bulk-assign-instructor")]
    [RequirePermission("menu.ogretim-elemani-atama.bulk-assign")]
    public async Task<IActionResult> BulkAssignInstructor(
        [FromQuery] int departmentId, [FromQuery] int termId,
        [FromBody] BulkAssignInstructorRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _instructorAssignmentService.BulkAssignInstructorAsync(departmentId, termId, request, ct);
        return Ok(result);
    }

    [HttpGet("instructors/{instructorId}/workload")]
    [RequireAnyPermission("menu.ogretim-elemani-atama.view", "menu.ders-veren-ogretim-uyeleri.view")]
    public async Task<IActionResult> GetInstructorWorkload(
        int instructorId,
        [FromQuery] int termId,
        [FromQuery] int? departmentId,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.Id == instructorId, ct);

        if (instructor == null)
            return NotFound(new { error = "Öğretim üyesi bulunamadı." });

        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToList();
        IReadOnlyCollection<int> scopedDepartmentIds;

        if (departmentId.HasValue)
        {
            if (!await HasDepartmentAccessAsync(userId, departmentId.Value, ct))
                return Forbid();

            var requestedFacultyId = await _context.Departments
                .AsNoTracking()
                .Where(department => department.Id == departmentId.Value)
                .Select(department => (int?)department.FacultyId)
                .FirstOrDefaultAsync(ct);

            if (!requestedFacultyId.HasValue)
                return NotFound(new { error = "Bölüm bulunamadı." });

            scopedDepartmentIds = [departmentId.Value];

            if (instructor.Department?.FacultyId != requestedFacultyId.Value
                && !await InstructorHasVisibleSectionAsync(instructorId, termId, scopedDepartmentIds, ct))
                return Forbid();
        }
        else
        {
            scopedDepartmentIds = await ResolveAccessibleDepartmentIdsAsync(userId, roles, ct);
            if (scopedDepartmentIds.Count == 0)
                return Forbid();

            var instructorHasVisibleSection = scopedDepartmentIds.Contains(instructor.DepartmentId)
                || await InstructorHasVisibleSectionAsync(instructorId, termId, scopedDepartmentIds, ct);

            if (!instructorHasVisibleSection)
                return Forbid();
        }

        var scopedDepartmentIdList = scopedDepartmentIds.ToList();
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == instructor.UserId, ct);

        var fullName = user != null
            ? $"{instructor.Title} {user.FirstName} {user.LastName}"
            : $"{instructor.Title} (#{instructor.Id})";

        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course).ThenInclude(course => course.Department)
            .Where(cs => cs.InstructorId == instructorId
                && cs.TermId == termId
                && cs.Status == CourseSectionStatus.Active
                && ((cs.Course.DepartmentId.HasValue && scopedDepartmentIdList.Contains(cs.Course.DepartmentId.Value))
                    || _context.CourseAccessPolicies.Any(cap =>
                        cap.CourseId == cs.CourseId
                        && cap.TermId == termId
                        && cap.DepartmentId.HasValue
                        && scopedDepartmentIdList.Contains(cap.DepartmentId.Value))))
            .OrderBy(cs => cs.Course.Code).ThenBy(cs => cs.SectionNumber)
            .ToListAsync(ct);

        var assignedSections = sections.Select(cs => new AssignedSectionDto
        {
            SectionId = cs.Id,
            CourseCode = cs.Course.Code,
            CourseName = cs.Course.Name,
            DepartmentId = cs.Course.DepartmentId,
            DepartmentName = cs.Course.Department?.Name,
            DepartmentCode = cs.Course.Department?.Code,
            SectionNumber = cs.SectionNumber,
            WeeklyHours = cs.Course.TheoryHours + cs.Course.LabHours,
            Capacity = cs.Capacity,
            EnrolledCount = cs.EnrolledCount,
        }).ToList();

        var assignedWeeklyHours = assignedSections.Sum(s => s.WeeklyHours);
        var weeklyHoursPolicy = await InstructorWeeklyHoursPolicy.LoadAsync(_systemConfig, ct);
        var effectiveMaxWeeklyHours = weeklyHoursPolicy.Resolve(instructor.Title, instructor.MaxWeeklyHours);

        return Ok(new InstructorWorkloadDto
        {
            InstructorId = instructor.Id,
            FullName = fullName,
            Title = instructor.Title,
            MaxWeeklyHours = effectiveMaxWeeklyHours,
            AssignedWeeklyHours = assignedWeeklyHours,
            RemainingCapacity = Math.Max(0, effectiveMaxWeeklyHours - assignedWeeklyHours),
            AssignedSections = assignedSections,
        });
    }

    // ── Geçmiş Dönemden Aktarım (Ders Aç (Hareket)) ──────────────────────

    [HttpGet("previous-term-courses")]
    [RequirePermission("menu.donem-programi-aktarim.view")]
    public async Task<IActionResult> GetPreviousTermCourses(
        [FromQuery] int departmentId,
        [FromQuery] int sourceTermId,
        [FromQuery] int targetTermId,
        [FromQuery] int? programId,
        [FromQuery] int? classLevel,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, departmentId, ct)) return Forbid();

        var result = await _transferService.GetPreviousTermCoursesAsync(
            departmentId, sourceTermId, targetTermId, programId, classLevel, ct);
        return Ok(result);
    }

    [HttpPost("transfer-from-term")]
    [RequirePermission("menu.donem-programi-aktarim.transfer")]
    public async Task<IActionResult> TransferFromTerm([FromBody] TransferFromTermRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasDepartmentAccessAsync(userId, request.DepartmentId, ct)) return Forbid();

        var result = await _transferService.TransferFromTermAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("transfer-defaults")]
    [RequirePermission("menu.donem-programi-aktarim.view")]
    public async Task<IActionResult> GetTransferDefaults(CancellationToken ct)
    {
        var defaultCapacity = await _systemConfig.GetIntAsync("CourseOpening.DefaultCapacityPerSection", 40, ct);
        var maxSections = await _systemConfig.GetIntAsync("CourseOpening.MaxSectionsPerCourse", 20, ct);
        return Ok(new TransferDefaultsDto(defaultCapacity, maxSections));
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim, out userId);
    }

    private async Task<List<int>> ResolveAccessibleDepartmentIdsAsync(int userId, IReadOnlyCollection<string> roles, CancellationToken ct)
    {
        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff"))
        {
            return await _context.Departments
                .AsNoTracking()
                .OrderBy(department => department.Name)
                .Select(department => department.Id)
                .ToListAsync(ct);
        }

        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(current => current.Department)
            .FirstOrDefaultAsync(current => current.UserId == userId, ct);

        if (instructor == null)
            return [];

        if (roles.Contains("Dean"))
        {
            return await _context.Departments
                .AsNoTracking()
                .Where(department => department.FacultyId == instructor.Department.FacultyId)
                .OrderBy(department => department.Name)
                .Select(department => department.Id)
                .ToListAsync(ct);
        }

        if (roles.Contains("DepartmentManager") || roles.Contains("Instructor") || roles.Contains("AcademicAdvisor"))
            return [instructor.DepartmentId];

        return [];
    }

    private async Task<bool> InstructorHasVisibleSectionAsync(int instructorId, int termId, IReadOnlyCollection<int> departmentIds, CancellationToken ct)
    {
        var scopedDepartmentIds = departmentIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (scopedDepartmentIds.Count == 0)
            return false;

        return await _context.CourseSections
            .AsNoTracking()
            .AnyAsync(cs => cs.InstructorId == instructorId
                && cs.TermId == termId
                && cs.Status == CourseSectionStatus.Active
                && ((cs.Course.DepartmentId.HasValue && scopedDepartmentIds.Contains(cs.Course.DepartmentId.Value))
                    || _context.CourseAccessPolicies.Any(cap =>
                        cap.CourseId == cs.CourseId
                        && cap.TermId == termId
                        && cap.DepartmentId.HasValue
                        && scopedDepartmentIds.Contains(cap.DepartmentId.Value))), ct);
    }

    private async Task<bool> HasDepartmentAccessAsync(int userId, int departmentId, CancellationToken ct)
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff"))
            return true;

        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.UserId == userId, ct);

        if (instructor == null) return false;

        if (roles.Contains("Dean"))
        {
            var department = await _context.Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == departmentId, ct);
            return department != null && department.FacultyId == instructor.Department.FacultyId;
        }

        if (roles.Contains("DepartmentManager") || roles.Contains("Instructor") || roles.Contains("AcademicAdvisor"))
            return instructor.DepartmentId == departmentId;

        return false;
    }
}
