using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.AcademicOperations.Services;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "AdministrativeStaff,DepartmentManager,Dean,SystemAdministrator")]
public class SchedulingController : ControllerBase
{
    private static readonly JsonSerializerOptions ScheduleJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ISchedulingService _schedulingService;
    private readonly IAcademicOperationsService _academicOperationsService;
    private readonly AppDbContext _context;

    public SchedulingController(
        ISchedulingService schedulingService,
        IAcademicOperationsService academicOperationsService,
        AppDbContext context)
    {
        _schedulingService = schedulingService;
        _academicOperationsService = academicOperationsService;
        _context = context;
    }

    [HttpGet("conflicts")]
    public async Task<IActionResult> GetConflicts([FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue && termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        var conflicts = await _schedulingService.GetConflictsAsync(termId, cancellationToken);
        return Ok(conflicts);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue && termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        var dashboard = await _schedulingService.GetClassroomDashboardAsync(termId, cancellationToken);
        return Ok(dashboard);
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ScheduleValidationRequestDto request, CancellationToken cancellationToken)
    {
        if (request.SectionId <= 0)
            return BadRequest(new { error = "Gecerli bir sectionId gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out _))
            return Unauthorized();
        var accessCheck = await CheckSectionAccessAsync(request.SectionId, userId, cancellationToken);
        if (accessCheck != null) return accessCheck;

        var result = await _schedulingService.ValidateAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("suggest")]
    public async Task<IActionResult> Suggest([FromBody] ScheduleSuggestionRequestDto request, CancellationToken cancellationToken)
    {
        if (request.SectionId <= 0)
            return BadRequest(new { error = "Gecerli bir sectionId gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out _))
            return Unauthorized();
        var accessCheck = await CheckSectionAccessAsync(request.SectionId, userId, cancellationToken);
        if (accessCheck != null) return accessCheck;

        var result = await _schedulingService.SuggestAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] ApplyScheduleRequestDto request, CancellationToken cancellationToken)
    {
        if (request.SectionId <= 0)
            return BadRequest(new { error = "Gecerli bir sectionId gonderilmelidir." });

        if (request.ScheduleSlots == null || request.ScheduleSlots.Count == 0)
            return BadRequest(new { error = "En az bir schedule slot gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out _))
            return Unauthorized();
        var accessCheck = await CheckSectionAccessAsync(request.SectionId, userId, cancellationToken);
        if (accessCheck != null) return accessCheck;

        var result = await _schedulingService.ApplyAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("validate-existing")]
    public async Task<IActionResult> ValidateExisting([FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue && termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        var result = await _schedulingService.ValidateExistingAsync(termId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("batch-auto-schedule")]
    public async Task<IActionResult> BatchAutoSchedule([FromBody] BatchAutoScheduleRequestDto request, CancellationToken cancellationToken)
    {
        if (request.TermId.HasValue && request.TermId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        if (request.DepartmentId.HasValue && request.DepartmentId <= 0)
            return BadRequest(new { error = "Gecerli bir departmentId gonderilmelidir." });

        if (request.FacultyId.HasValue && request.FacultyId <= 0)
            return BadRequest(new { error = "Gecerli bir facultyId gonderilmelidir." });

        if (request.DepartmentId.HasValue && request.FacultyId.HasValue)
            return BadRequest(new { error = "departmentId ve facultyId ayni anda gonderilemez." });

        if (!TryGetAuthenticatedUser(out var userId, out var roles))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var context = await _academicOperationsService.GetContextAsync(userId, roles, cancellationToken);

        if (request.DepartmentId.HasValue)
        {
            var hasDepartmentAccess = context.AccessibleDepartments
                .Any(department => department.DepartmentId == request.DepartmentId.Value);

            if (!hasDepartmentAccess)
                return Forbid();
        }
        else if (!context.CanViewAllDepartments)
        {
            if (!context.DefaultDepartmentId.HasValue)
                return BadRequest(new { error = "Bu kullanici icin erisilebilir bolum bulunamadi." });

            request.DepartmentId = context.DefaultDepartmentId.Value;
        }

        var result = await _schedulingService.BatchAutoScheduleAsync(request, cancellationToken);
        return Ok(result);
    }

    // ── Swap Endpoint ────────────────────────────────────────────────────

    [HttpPost("swap")]
    [Authorize(Roles = "DepartmentManager,Dean,SystemAdministrator")]
    public async Task<IActionResult> SwapSections([FromBody] SwapSectionsRequestDto request, CancellationToken cancellationToken)
    {
        if (request.SectionId1 <= 0 || request.SectionId2 <= 0)
            return BadRequest(new { error = "Gecerli section ID'leri gonderilmelidir." });

        if (request.SectionId1 == request.SectionId2)
            return BadRequest(new { error = "Ayni section kendi kendisiyle takas edilemez." });

        if (!request.SwapSlots && !request.SwapClassrooms)
            return BadRequest(new { error = "En az bir takas secenegi (SwapSlots veya SwapClassrooms) secilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out _))
            return Unauthorized();

        // Access check for both sections
        var access1 = await CheckSectionAccessAsync(request.SectionId1, userId, cancellationToken);
        if (access1 != null) return access1;
        var access2 = await CheckSectionAccessAsync(request.SectionId2, userId, cancellationToken);
        if (access2 != null) return access2;

        // Load both sections with related data
        var section1 = await _context.CourseSections
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId1, cancellationToken);

        var section2 = await _context.CourseSections
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId2, cancellationToken);

        if (section1 == null || section2 == null)
            return NotFound(new { error = "Belirtilen section'lardan biri veya ikisi bulunamadi." });

        if (section1.TermId != section2.TermId)
            return BadRequest(new { error = "Farkli donemlerdeki section'lar takas edilemez." });

        // Load SectionSchedule records for both sections
        var swapSchedules1 = await _context.SectionSchedules
            .Include(ss => ss.TimeSlot)
            .Where(ss => ss.CourseSectionId == section1.Id)
            .ToListAsync(cancellationToken);
        var swapSchedules2 = await _context.SectionSchedules
            .Include(ss => ss.TimeSlot)
            .Where(ss => ss.CourseSectionId == section2.Id)
            .ToListAsync(cancellationToken);

        // Preserve originals for rollback
        var origScheduleJson1 = section1.ScheduleJson;
        var origScheduleJson2 = section2.ScheduleJson;
        var origClassroomId1 = section1.ClassroomId;
        var origClassroomId2 = section2.ClassroomId;

        // Perform swap
        if (request.SwapSlots)
        {
            section1.ScheduleJson = origScheduleJson2;
            section2.ScheduleJson = origScheduleJson1;
        }

        if (request.SwapClassrooms)
        {
            section1.ClassroomId = origClassroomId2;
            section2.ClassroomId = origClassroomId1;
        }

        // Build validation slots from SectionSchedule (swapped if needed)
        var slotsSource1 = request.SwapSlots ? swapSchedules2 : swapSchedules1;
        var slotsSource2 = request.SwapSlots ? swapSchedules1 : swapSchedules2;
        var slots1 = slotsSource1.Select(ss => new ScheduleSlotDto
        {
            Day = ss.TimeSlot.DayOfWeek,
            StartTime = ss.TimeSlot.StartTime,
            EndTime = ss.TimeSlot.EndTime,
            ClassroomId = ss.ClassroomId
        }).ToList();
        var slots2 = slotsSource2.Select(ss => new ScheduleSlotDto
        {
            Day = ss.TimeSlot.DayOfWeek,
            StartTime = ss.TimeSlot.StartTime,
            EndTime = ss.TimeSlot.EndTime,
            ClassroomId = ss.ClassroomId
        }).ToList();

        var validation1 = await _schedulingService.ValidateAsync(new ScheduleValidationRequestDto
        {
            SectionId = section1.Id,
            InstructorId = section1.InstructorId,
            ClassroomId = section1.ClassroomId,
            ScheduleSlots = slots1
        }, cancellationToken);

        var validation2 = await _schedulingService.ValidateAsync(new ScheduleValidationRequestDto
        {
            SectionId = section2.Id,
            InstructorId = section2.InstructorId,
            ClassroomId = section2.ClassroomId,
            ScheduleSlots = slots2
        }, cancellationToken);

        // Check for hard conflicts
        if (!validation1.IsValid || !validation2.IsValid)
        {
            // Rollback
            section1.ScheduleJson = origScheduleJson1;
            section2.ScheduleJson = origScheduleJson2;
            section1.ClassroomId = origClassroomId1;
            section2.ClassroomId = origClassroomId2;

            var allConflicts = new List<SchedulingConflictDto>();
            allConflicts.AddRange(validation1.Conflicts);
            allConflicts.AddRange(validation2.Conflicts);

            return Conflict(new
            {
                error = "Takas sonrasinda cakisma tespit edildi. Takas iptal edildi.",
                conflicts = allConflicts
            });
        }

        // Update SectionSchedule records for both sections
        await UpdateSectionScheduleRecordsAsync(section1, cancellationToken);
        await UpdateSectionScheduleRecordsAsync(section2, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Takas basariyla uygulanadi.",
            section1 = BuildSectionState(section1),
            section2 = BuildSectionState(section2)
        });
    }

    // ── Lock / Unlock Endpoints ──────────────────────────────────────────

    [HttpPost("lock/{sectionId}")]
    [Authorize(Roles = "DepartmentManager,Dean,SystemAdministrator")]
    public async Task<IActionResult> LockSection(int sectionId, CancellationToken cancellationToken)
    {
        if (sectionId <= 0)
            return BadRequest(new { error = "Gecerli bir sectionId gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out _))
            return Unauthorized();
        var accessCheck = await CheckSectionAccessAsync(sectionId, userId, cancellationToken);
        if (accessCheck != null) return accessCheck;

        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .Include(cs => cs.Classroom)
            .FirstOrDefaultAsync(cs => cs.Id == sectionId, cancellationToken);

        if (section == null)
            return NotFound(new { error = "Section bulunamadi." });

        section.IsScheduleLocked = true;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Section basariyla kilitlendi.",
            section = BuildSectionState(section)
        });
    }

    [HttpPost("unlock/{sectionId}")]
    [Authorize(Roles = "DepartmentManager,Dean,SystemAdministrator")]
    public async Task<IActionResult> UnlockSection(int sectionId, CancellationToken cancellationToken)
    {
        if (sectionId <= 0)
            return BadRequest(new { error = "Gecerli bir sectionId gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out _))
            return Unauthorized();
        var accessCheck = await CheckSectionAccessAsync(sectionId, userId, cancellationToken);
        if (accessCheck != null) return accessCheck;

        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .Include(cs => cs.Classroom)
            .FirstOrDefaultAsync(cs => cs.Id == sectionId, cancellationToken);

        if (section == null)
            return NotFound(new { error = "Section bulunamadi." });

        section.IsScheduleLocked = false;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Section kilidi basariyla kaldirildi.",
            section = BuildSectionState(section)
        });
    }

    // ── Timetable Endpoints ──────────────────────────────────────────────

    [HttpGet("instructor/{instructorId}/timetable")]
    [Authorize(Roles = "DepartmentManager,Dean,AdministrativeStaff,SystemAdministrator")]
    public async Task<IActionResult> GetInstructorTimetable(int instructorId, [FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (instructorId <= 0)
            return BadRequest(new { error = "Gecerli bir instructorId gonderilmelidir." });

        var term = await ResolveTermAsync(termId, cancellationToken);
        if (term == null)
            return NotFound(new { error = "Aktif akademik donem bulunamadi." });

        var instructor = await _context.Instructors
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == instructorId, cancellationToken);

        if (instructor == null)
            return NotFound(new { error = "Ogretim uyesi bulunamadi." });

        var instructorName = await GetInstructorDisplayNameAsync(instructor, cancellationToken);

        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Classroom)
            .Where(cs => cs.TermId == term.Id && cs.InstructorId == instructorId && cs.IsActive)
            .ToListAsync(cancellationToken);

        // Load SectionSchedule records for timetable
        var instrSectionIds = sections.Select(s => s.Id).ToList();
        var instrSchedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => instrSectionIds.Contains(ss.CourseSectionId))
            .ToListAsync(cancellationToken);
        var instrScheduleMap = instrSchedules
            .GroupBy(ss => ss.CourseSectionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var entries = new List<TimetableEntryDto>();
        foreach (var section in sections)
        {
            var scheds = instrScheduleMap.GetValueOrDefault(section.Id) ?? new();
            foreach (var ss in scheds)
            {
                entries.Add(new TimetableEntryDto
                {
                    Day = ss.TimeSlot.DayOfWeek,
                    StartTime = ss.TimeSlot.StartTime,
                    EndTime = ss.TimeSlot.EndTime,
                    CourseCode = section.Course.Code,
                    CourseName = section.Course.Name,
                    SectionId = section.Id,
                    SectionNumber = section.SectionNumber,
                    ClassroomName = section.Classroom?.Name,
                    InstructorName = instructorName
                });
            }
        }

        return Ok(entries.OrderBy(e => e.Day).ThenBy(e => e.StartTime));
    }

    [HttpGet("classroom/{classroomId}/timetable")]
    [Authorize(Roles = "DepartmentManager,Dean,AdministrativeStaff,SystemAdministrator")]
    public async Task<IActionResult> GetClassroomTimetable(int classroomId, [FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (classroomId <= 0)
            return BadRequest(new { error = "Gecerli bir classroomId gonderilmelidir." });

        var term = await ResolveTermAsync(termId, cancellationToken);
        if (term == null)
            return NotFound(new { error = "Aktif akademik donem bulunamadi." });

        var classroom = await _context.Classrooms
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == classroomId, cancellationToken);

        if (classroom == null)
            return NotFound(new { error = "Derslik bulunamadi." });

        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Where(cs => cs.TermId == term.Id && cs.ClassroomId == classroomId && cs.IsActive)
            .ToListAsync(cancellationToken);

        var instructorIds = sections.Where(s => s.InstructorId.HasValue).Select(s => s.InstructorId!.Value).Distinct().ToList();
        var instructorNames = await LoadInstructorNameMapAsync(instructorIds, cancellationToken);

        // Load SectionSchedule records for classroom timetable
        var classroomSectionIds = sections.Select(s => s.Id).ToList();
        var classroomSchedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => classroomSectionIds.Contains(ss.CourseSectionId))
            .ToListAsync(cancellationToken);
        var classroomScheduleMap = classroomSchedules
            .GroupBy(ss => ss.CourseSectionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var entries = new List<TimetableEntryDto>();
        foreach (var section in sections)
        {
            var scheds = classroomScheduleMap.GetValueOrDefault(section.Id) ?? new();
            foreach (var ss in scheds)
            {
                entries.Add(new TimetableEntryDto
                {
                    Day = ss.TimeSlot.DayOfWeek,
                    StartTime = ss.TimeSlot.StartTime,
                    EndTime = ss.TimeSlot.EndTime,
                    CourseCode = section.Course.Code,
                    CourseName = section.Course.Name,
                    SectionId = section.Id,
                    SectionNumber = section.SectionNumber,
                    ClassroomName = classroom.Name,
                    InstructorName = section.InstructorId.HasValue && instructorNames.TryGetValue(section.InstructorId.Value, out var name)
                        ? name : null
                });
            }
        }

        return Ok(entries.OrderBy(e => e.Day).ThenBy(e => e.StartTime));
    }

    [HttpGet("department/{departmentId}/timetable")]
    [Authorize(Roles = "DepartmentManager,Dean,AdministrativeStaff,SystemAdministrator")]
    public async Task<IActionResult> GetDepartmentTimetable(int departmentId, [FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (departmentId <= 0)
            return BadRequest(new { error = "Gecerli bir departmentId gonderilmelidir." });

        var term = await ResolveTermAsync(termId, cancellationToken);
        if (term == null)
            return NotFound(new { error = "Aktif akademik donem bulunamadi." });

        var department = await _context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == departmentId, cancellationToken);

        if (department == null)
            return NotFound(new { error = "Bolum bulunamadi." });

        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .Where(cs => cs.TermId == term.Id && cs.Course.DepartmentId == departmentId && cs.IsActive)
            .ToListAsync(cancellationToken);

        var instructorIds = sections.Where(s => s.InstructorId.HasValue).Select(s => s.InstructorId!.Value).Distinct().ToList();
        var instructorNames = await LoadInstructorNameMapAsync(instructorIds, cancellationToken);

        // Get curriculum course year mappings for this department
        var courseIds = sections.Select(s => s.CourseId).Distinct().ToList();
        var curriculumCourses = await _context.CurriculumCourses
            .AsNoTracking()
            .Include(cc => cc.Curriculum)
                .ThenInclude(c => c.Program)
            .Where(cc => courseIds.Contains(cc.CourseId) && cc.Curriculum.Program.DepartmentId == departmentId)
            .ToListAsync(cancellationToken);

        // Map courseId to year level (SuggestedSemester / 2 + 1, rounded up)
        var courseYearMap = curriculumCourses
            .GroupBy(cc => cc.CourseId)
            .ToDictionary(
                g => g.Key,
                g => (int)Math.Ceiling(g.Min(cc => cc.SuggestedSemester) / 2.0)
            );

        // Load SectionSchedule records for department timetable
        var deptSectionIds = sections.Select(s => s.Id).ToList();
        var deptSchedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => deptSectionIds.Contains(ss.CourseSectionId))
            .ToListAsync(cancellationToken);
        var deptScheduleMap = deptSchedules
            .GroupBy(ss => ss.CourseSectionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var yearGroups = new Dictionary<int, List<TimetableEntryDto>>();

        foreach (var section in sections)
        {
            var year = courseYearMap.TryGetValue(section.CourseId, out var y) ? y : 0;
            var scheds = deptScheduleMap.GetValueOrDefault(section.Id) ?? new();

            if (!yearGroups.ContainsKey(year))
                yearGroups[year] = new List<TimetableEntryDto>();

            foreach (var ss in scheds)
            {
                yearGroups[year].Add(new TimetableEntryDto
                {
                    Day = ss.TimeSlot.DayOfWeek,
                    StartTime = ss.TimeSlot.StartTime,
                    EndTime = ss.TimeSlot.EndTime,
                    CourseCode = section.Course.Code,
                    CourseName = section.Course.Name,
                    SectionId = section.Id,
                    SectionNumber = section.SectionNumber,
                    ClassroomName = section.Classroom?.Name,
                    InstructorName = section.InstructorId.HasValue && instructorNames.TryGetValue(section.InstructorId.Value, out var name)
                        ? name : null
                });
            }
        }

        var result = yearGroups
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new DepartmentTimetableYearGroupDto
            {
                Year = kvp.Key,
                Courses = kvp.Value.OrderBy(e => e.Day).ThenBy(e => e.StartTime).ToList()
            })
            .ToList();

        return Ok(result);
    }

    // ── Auto-Resolve Endpoint ────────────────────────────────────────────

    [HttpPost("auto-resolve")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> AutoResolve([FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue && termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        // Step 1: Get existing conflicts
        var validation = await _schedulingService.ValidateExistingAsync(termId, cancellationToken);

        if (validation.IsValid)
        {
            return Ok(new AutoResolveResultDto
            {
                ResolvedCount = 0,
                RemainingConflictCount = 0,
                Notes = { "Mevcut programda cakisma bulunmamaktadir." }
            });
        }

        // Get distinct conflicting section IDs
        var conflictingSectionIds = validation.Conflicts
            .SelectMany(c => new[] { c.SectionId, c.RelatedSectionId ?? 0 })
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var result = new AutoResolveResultDto();

        // Step 2: For each conflicting section, try to find and apply an alternative
        foreach (var sectionId in conflictingSectionIds)
        {
            // Check if section is locked
            var section = await _context.CourseSections
                .AsNoTracking()
                .FirstOrDefaultAsync(cs => cs.Id == sectionId, cancellationToken);

            if (section == null || section.IsScheduleLocked)
                continue;

            var suggestion = await _schedulingService.SuggestAsync(new ScheduleSuggestionRequestDto
            {
                SectionId = sectionId
            }, cancellationToken);

            if (!suggestion.IsFeasible || suggestion.SuggestedSlots.Count == 0)
                continue;

            var applyResult = await _schedulingService.ApplyAsync(new ApplyScheduleRequestDto
            {
                SectionId = sectionId,
                ClassroomId = suggestion.SuggestedClassroomId,
                ScheduleSlots = suggestion.SuggestedSlots
            }, cancellationToken);

            if (applyResult.IsValid)
            {
                result.ResolvedCount++;
                result.ResolvedSections.Add(new AutoResolveItemDto
                {
                    SectionId = sectionId,
                    CourseCode = suggestion.Notes.FirstOrDefault(n => n.Contains("Ders:"))?.Replace("Ders: ", "") ?? "",
                    CourseName = "",
                    NewClassroomName = suggestion.SuggestedClassroomName,
                    NewSlots = suggestion.SuggestedSlots
                });
            }
        }

        // Step 3: Re-validate to find remaining conflicts
        var finalValidation = await _schedulingService.ValidateExistingAsync(termId, cancellationToken);
        result.RemainingConflicts = finalValidation.Conflicts;
        result.RemainingConflictCount = finalValidation.Conflicts.Count;
        result.Notes.Add($"{result.ResolvedCount} section basariyla yeniden programlandi.");

        if (result.RemainingConflictCount > 0)
            result.Notes.Add($"{result.RemainingConflictCount} cakisma hala cozulemedi, manuel mudahale gereklidir.");

        return Ok(result);
    }

    // ── Private Helpers ──────────────────────────────────────────────────

    private async Task<Domain.Entities.AcademicTerm?> ResolveTermAsync(int? termId, CancellationToken ct)
    {
        if (termId.HasValue)
        {
            return await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == termId.Value, ct);
        }

        return await _context.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IsActive, ct);
    }

    private async Task<string> GetInstructorDisplayNameAsync(Domain.Entities.Instructor instructor, CancellationToken ct)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == instructor.UserId, ct);

        return user == null
            ? instructor.Title
            : $"{instructor.Title} {user.FirstName} {user.LastName}".Trim();
    }

    private async Task<Dictionary<int, string>> LoadInstructorNameMapAsync(List<int> instructorIds, CancellationToken ct)
    {
        if (instructorIds.Count == 0)
            return new Dictionary<int, string>();

        var instructors = await _context.Instructors
            .AsNoTracking()
            .Where(i => instructorIds.Contains(i.Id))
            .ToListAsync(ct);

        var userIds = instructors.Select(i => i.UserId).ToList();
        var users = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync(ct);

        var userMap = users.ToDictionary(u => u.Id);

        return instructors.ToDictionary(
            i => i.Id,
            i => userMap.TryGetValue(i.UserId, out var user)
                ? $"{i.Title} {user.FirstName} {user.LastName}".Trim()
                : i.Title
        );
    }

    private async Task UpdateSectionScheduleRecordsAsync(Domain.Entities.CourseSection section, CancellationToken ct)
    {
        // Remove old SectionSchedule records
        var existing = await _context.SectionSchedules
            .Where(ss => ss.CourseSectionId == section.Id)
            .ToListAsync(ct);

        if (existing.Count > 0)
            _context.SectionSchedules.RemoveRange(existing);

        if (!section.ClassroomId.HasValue)
            return;

        var slots = JsonSerializer.Deserialize<List<ScheduleSlot>>(section.ScheduleJson, ScheduleJsonOptions);
        if (slots == null || slots.Count == 0)
            return;

        var termTimeSlots = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == section.TermId)
            .ToListAsync(ct);

        foreach (var slot in slots)
        {
            var matchingTimeSlots = termTimeSlots
                .Where(ts => ts.DayOfWeek == slot.Day
                    && ts.StartTime < slot.EndTime
                    && slot.StartTime < ts.EndTime)
                .OrderBy(ts => ts.SlotOrder)
                .ToList();

            foreach (var ts in matchingTimeSlots)
            {
                _context.SectionSchedules.Add(new Domain.Entities.SectionSchedule
                {
                    CourseSectionId = section.Id,
                    TimeSlotId = ts.Id,
                    ClassroomId = section.ClassroomId.Value
                });
            }
        }
    }

    private static object BuildSectionState(Domain.Entities.CourseSection section)
    {
        return new
        {
            sectionId = section.Id,
            courseCode = section.Course.Code,
            courseName = section.Course.Name,
            classroomId = section.ClassroomId,
            classroomName = section.Classroom?.Name,
            scheduleJson = section.ScheduleJson,
            isScheduleLocked = section.IsScheduleLocked,
            sectionNumber = section.SectionNumber
        };
    }

    private async Task<IActionResult?> CheckSectionAccessAsync(int sectionId, int userId, CancellationToken ct)
    {
        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == sectionId, ct);

        if (section == null)
            return NotFound(new { error = "Şube bulunamadı." });

        if (!section.Course.DepartmentId.HasValue)
            return null; // Ortak ders — herkes erişebilir

        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff"))
            return null;

        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.UserId == userId, ct);

        if (instructor == null)
            return Forbid();

        if (roles.Contains("Dean"))
        {
            var department = await _context.Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == section.Course.DepartmentId.Value, ct);
            if (department == null || department.FacultyId != instructor.Department.FacultyId)
                return Forbid();
            return null;
        }

        if (roles.Contains("DepartmentManager"))
        {
            if (instructor.DepartmentId != section.Course.DepartmentId.Value)
                return Forbid();
            return null;
        }

        return Forbid();
    }

    private bool TryGetAuthenticatedUser(out int userId, out IReadOnlyCollection<string> roles)
    {
        userId = 0;
        roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToList();

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userIdClaim != null && int.TryParse(userIdClaim, out userId);
    }
}
