using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.AcademicOperations.Services;
using SIS.Application.Notifications.Services;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SchedulingController : ControllerBase
{
    private readonly ISchedulingService _schedulingService;
    private readonly IAcademicOperationsService _academicOperationsService;
    private readonly AppDbContext _context;
    private readonly INotificationService? _notifications;

    public SchedulingController(
        ISchedulingService schedulingService,
        IAcademicOperationsService academicOperationsService,
        AppDbContext context,
        INotificationService? notifications = null)
    {
        _schedulingService = schedulingService;
        _academicOperationsService = academicOperationsService;
        _context = context;
        _notifications = notifications;
    }

    [HttpGet("conflicts")]
    [RequirePermission("menu.derslik-doluluk.view")]
    public async Task<IActionResult> GetConflicts([FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue && termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        var conflicts = await _schedulingService.GetConflictsAsync(termId, cancellationToken);
        return Ok(conflicts);
    }

    [HttpGet("dashboard")]
    [RequirePermission("menu.derslik-doluluk.view")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int? termId,
        [FromQuery] int? facultyId,
        [FromQuery] string? buildingName,
        [FromQuery] string? classroomType,
        [FromQuery] int? minCapacity,
        [FromQuery] int? maxCapacity,
        CancellationToken cancellationToken)
    {
        if (termId.HasValue && termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        return Ok(await _schedulingService.GetClassroomDashboardAsync(
            termId, facultyId, buildingName, classroomType, minCapacity, maxCapacity, cancellationToken));
    }

    [HttpGet("available-classrooms")]
    [RequirePermission("menu.derslik-doluluk.view")]
    public async Task<IActionResult> SearchAvailableClassrooms(
        [FromQuery] int termId,
        [FromQuery] string days,
        [FromQuery] string startTime,
        [FromQuery] string endTime,
        [FromQuery] int? minCapacity,
        [FromQuery] string? classroomType,
        [FromQuery] int? facultyId,
        [FromQuery] string? buildingName,
        CancellationToken cancellationToken)
    {
        if (termId <= 0) return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });
        if (string.IsNullOrWhiteSpace(days)) return BadRequest(new { error = "En az bir gun secilmelidir." });

        // Gün değerleri System.DayOfWeek kodlaması (Pazar=0 … Cumartesi=6). Hafta sonu (Cmt/Paz)
        // slotları da oluşturulabildiği için arama tüm haftaya açık — eski 1-5 clamp'i hafta sonu
        // slotlarını programlanamaz bırakıyordu (TimeSlot oluşturma ile çelişki).
        var dayList = days.Split(',').Select(d => int.TryParse(d.Trim(), out var v) ? v : -1).Where(d => d >= 0 && d <= 6).ToList();
        if (dayList.Count == 0) return BadRequest(new { error = "Gecerli gun degerleri gonderilmelidir (0-6)." });

        if (!TimeOnly.TryParse(startTime, out var start) || !TimeOnly.TryParse(endTime, out var end))
            return BadRequest(new { error = "Baslangic/bitis saati gecerli formatta olmalidir (HH:mm)." });
        if (end <= start)
            return BadRequest(new { error = "Bitis saati baslangictan sonra olmalidir." });

        return Ok(await _schedulingService.SearchAvailableClassroomsAsync(
            termId, dayList, start, end, minCapacity, classroomType, facultyId, buildingName, cancellationToken));
    }

    [HttpGet("classroom-filters")]
    [RequirePermission("menu.derslik-doluluk.view")]
    public async Task<IActionResult> GetClassroomFilters([FromQuery] int termId, CancellationToken cancellationToken)
    {
        if (termId <= 0) return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });
        return Ok(await _schedulingService.GetClassroomFilterOptionsAsync(termId, cancellationToken));
    }

    [HttpPost("validate")]
    [RequirePermission("menu.ders-programi-olusturma.validate")]
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
    [RequirePermission("menu.ders-programi-olusturma.suggest")]
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
    [RequirePermission("menu.ders-programi-olusturma.apply")]
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
    [RequirePermission("menu.ders-programi-olusturma.validate")]
    public async Task<IActionResult> ValidateExisting([FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue && termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        var result = await _schedulingService.ValidateExistingAsync(termId, cancellationToken);
        return Ok(result);
    }

    // 2026-05-10: V1 batch-auto-schedule endpoint silindi (ölü kod). Faculty Planning UI
    // /faculty-planning/auto-schedule (V2 CP-SAT) kullanıyor. Bu endpoint hiçbir frontend'den
    // çağrılmıyordu, BatchAuto.cs ~500 satır V1 backtracking mantığı atıl kalmıştı.

    // ── Swap Endpoint ────────────────────────────────────────────────────

    [HttpPost("swap")]
    [RequirePermission("menu.ders-programi-olusturma.swap")]
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

        // Yayın kilidi: section'lardan birinin fakültesi o dönem için onaylanmış (yayında) ise
        // takas yapılamaz. Önce Program Onayı geri alınmalı. Ortak ders (DepartmentId null) muaf.
        var swapDeptIds = new[] { section1.Course.DepartmentId, section2.Course.DepartmentId }
            .Where(d => d.HasValue).Select(d => d!.Value).Distinct().ToList();
        var swapFacultyIds = await _context.Departments
            .Where(d => swapDeptIds.Contains(d.Id))
            .Select(d => d.FacultyId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (await _context.FacultyScheduleApprovals
            .AnyAsync(a => swapFacultyIds.Contains(a.FacultyId) && a.TermId == section1.TermId, cancellationToken))
            return BadRequest(new { error = "Bu fakültenin programı onaylanmış (yayında). Takas için önce Program Onayını geri alın." });

        // Load SectionSchedule records for both sections
        var swapSchedules1 = await _context.SectionSchedules
            .Include(ss => ss.TimeSlot)
            .Where(ss => ss.CourseSectionId == section1.Id)
            .ToListAsync(cancellationToken);
        var swapSchedules2 = await _context.SectionSchedules
            .Include(ss => ss.TimeSlot)
            .Where(ss => ss.CourseSectionId == section2.Id)
            .ToListAsync(cancellationToken);

        // Bağımsız takas (yalnız slot VEYA yalnız derslik) + farklı blok sayısı ill-defined:
        // SwapSectionScheduleRecordsAsync slot kaynağını (n adet) derslik kaynağıyla (m adet) zip'ler;
        // n≠m olunca derslikler yanlış slotlara eşlenir / bloklar düşer. Tam takas (ikisi birden) blok
        // sayısından bağımsız tutarlıdır. Bu yüzden yalnız tek-boyutlu takasta eşit blok sayısı zorunlu.
        if (request.SwapSlots != request.SwapClassrooms
            && swapSchedules1.Count != swapSchedules2.Count)
        {
            return BadRequest(new
            {
                error = "Farklı blok sayısına sahip section'lar yalnızca slot ve derslik birlikte takas edilebilir."
            });
        }

        // Preserve originals for rollback
        var origClassroomId1 = section1.ClassroomId;
        var origClassroomId2 = section2.ClassroomId;

        // Perform swap
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
            Day = (int)ss.TimeSlot.DayOfWeek,
            StartTime = ss.TimeSlot.StartTime,
            EndTime = ss.TimeSlot.EndTime,
            ClassroomId = ss.ClassroomId
        }).ToList();
        var slots2 = slotsSource2.Select(ss => new ScheduleSlotDto
        {
            Day = (int)ss.TimeSlot.DayOfWeek,
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
        await SwapSectionScheduleRecordsAsync(section1, section2, request.SwapSlots, request.SwapClassrooms, cancellationToken);

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
    [RequirePermission("menu.ders-programi-olusturma.lock")]
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
    [RequirePermission("menu.ders-programi-olusturma.unlock")]
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

    // ── Faz 3b: Yayın sonrası tek-section düzenleme (Stop / Resume Enrollment) ──────

    /// <summary>
    /// Onaylı (yayında) bir programda tek section'ı düzenlemek için "yeni kayda durdur".
    /// Section yeni öğrenci kaydına kapanır (mevcut kayıtlılar korunur) ve yayın kilidi
    /// yalnızca bu section için gevşer → assign/unassign yapılabilir.
    /// </summary>
    [HttpPost("suspend-enrollment/{sectionId}")]
    [RequirePermission("menu.ders-programi-olusturma.assign")]
    public async Task<IActionResult> SuspendEnrollment(int sectionId, CancellationToken cancellationToken)
    {
        if (sectionId <= 0)
            return BadRequest(new { error = "Gecerli bir sectionId gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out _))
            return Unauthorized();
        var accessCheck = await CheckSectionAccessAsync(sectionId, userId, cancellationToken);
        if (accessCheck != null) return accessCheck;

        var section = await _context.CourseSections
            .FirstOrDefaultAsync(cs => cs.Id == sectionId, cancellationToken);
        if (section == null)
            return NotFound(new { error = "Section bulunamadi." });

        section.IsEnrollmentSuspended = true;
        section.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Şube düzenleme için durduruldu (yeni kayda kapalı, mevcut kayıtlılar korunuyor).", sectionId, isEnrollmentSuspended = true });
    }

    /// <summary>
    /// Düzenlemeyi bitirir: section yeniden kayda açılır ve o section'a kayıtlı öğrencilere
    /// "ders programı güncellendi" bildirimi gönderilir.
    /// </summary>
    [HttpPost("resume-enrollment/{sectionId}")]
    [RequirePermission("menu.ders-programi-olusturma.assign")]
    public async Task<IActionResult> ResumeEnrollment(int sectionId, CancellationToken cancellationToken)
    {
        if (sectionId <= 0)
            return BadRequest(new { error = "Gecerli bir sectionId gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out _))
            return Unauthorized();
        var accessCheck = await CheckSectionAccessAsync(sectionId, userId, cancellationToken);
        if (accessCheck != null) return accessCheck;

        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == sectionId, cancellationToken);
        if (section == null)
            return NotFound(new { error = "Section bulunamadi." });

        section.IsEnrollmentSuspended = false;
        section.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        // Kayıtlı öğrencilere "programın güncellendi" bildirimi (yalnızca aktif/onay-bekleyen kayıtlar).
        var notified = 0;
        if (_notifications != null)
        {
            var studentUserIds = await _context.CourseRegistrations
                .Where(cr => cr.CourseSectionId == sectionId
                    && (cr.Status == RegistrationStatus.Active
                        || cr.Status == RegistrationStatus.PendingAdvisorApproval))
                .Join(_context.Students, cr => cr.StudentId, s => s.Id, (cr, s) => s.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (studentUserIds.Count > 0)
            {
                await _notifications.NotifyManyAsync(
                    studentUserIds,
                    NotificationType.ScheduleChanged,
                    "Ders programı güncellendi",
                    $"{section.Course.Code} {section.Course.Name} dersinin programı güncellendi. Lütfen yeni gün/saat/derslik bilgisini kontrol edin.",
                    link: "/ders-programim",
                    severity: NotificationSeverity.Info,
                    ct: cancellationToken);
                notified = studentUserIds.Count;
            }
        }

        return Ok(new { message = "Şube yeniden kayda açıldı.", sectionId, isEnrollmentSuspended = false, notifiedStudents = notified });
    }

    // ── Timetable Endpoints ──────────────────────────────────────────────

    [HttpGet("instructor/{instructorId}/timetable")]
    [RequirePermission("menu.ders-programi-olusturma.view")]
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
                    Day = (int)ss.TimeSlot.DayOfWeek,
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

    // ── Private Helpers ──────────────────────────────────────────────────

    private async Task<Domain.Entities.AcademicTerm?> ResolveTermAsync(int? termId, CancellationToken ct)
    {
        if (termId.HasValue)
        {
            return await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == termId.Value, ct);
        }

        var nowSched = DateTime.UtcNow;
        return await _context.AcademicTerms
            .AsNoTracking()
            .ActiveTerm()
            .FirstOrDefaultAsync(ct);
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

    private async Task SwapSectionScheduleRecordsAsync(
        Domain.Entities.CourseSection section1,
        Domain.Entities.CourseSection section2,
        bool swapSlots,
        bool swapClassrooms,
        CancellationToken ct)
    {
        var records1 = await _context.SectionSchedules
            .Where(ss => ss.CourseSectionId == section1.Id)
            .ToListAsync(ct);
        var records2 = await _context.SectionSchedules
            .Where(ss => ss.CourseSectionId == section2.Id)
            .ToListAsync(ct);

        // Capture original data before removal
        var data1 = records1.Select(r => new { r.TimeSlotId, r.ClassroomId }).ToList();
        var data2 = records2.Select(r => new { r.TimeSlotId, r.ClassroomId }).ToList();

        _context.SectionSchedules.RemoveRange(records1);
        _context.SectionSchedules.RemoveRange(records2);

        // Rebuild with swapped values
        var slots1 = swapSlots ? data2 : data1;
        var classrooms1 = swapClassrooms ? data2 : data1;
        var slots2 = swapSlots ? data1 : data2;
        var classrooms2 = swapClassrooms ? data1 : data2;

        for (int i = 0; i < slots1.Count; i++)
        {
            _context.SectionSchedules.Add(new Domain.Entities.SectionSchedule
            {
                CourseSectionId = section1.Id,
                TimeSlotId = slots1[i].TimeSlotId,
                ClassroomId = swapClassrooms && i < classrooms1.Count ? classrooms1[i].ClassroomId : slots1[i].ClassroomId
            });
        }

        for (int i = 0; i < slots2.Count; i++)
        {
            _context.SectionSchedules.Add(new Domain.Entities.SectionSchedule
            {
                CourseSectionId = section2.Id,
                TimeSlotId = slots2[i].TimeSlotId,
                ClassroomId = swapClassrooms && i < classrooms2.Count ? classrooms2[i].ClassroomId : slots2[i].ClassroomId
            });
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

        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff"))
            return null;

        if (!section.Course.DepartmentId.HasValue)
            // Ortak ders (bölümsüz) üniversite-geneli kaynaktır; fakülte/bölüm sahipliği çözülemez.
            // Yalnız SysAdmin/AdministrativeStaff yönetebilir (yukarıda izin verildi) — Dean/Bölüm
            // Başkanı bu section üzerinde lock/swap/validate yapamaz (eski "herkes erişebilir" açığı).
            return Forbid();

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
