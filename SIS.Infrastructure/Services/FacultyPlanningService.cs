using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.FacultyPlanning.Services;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Scheduling;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Fakülte planlama API'si — UI köprü servisi. Partial class:
///   - FacultyPlanningService.cs            (constructor, sorgular, dashboard, faculty access)
///   - FacultyPlanningService.Assignment.cs (AssignSection, ApplySchedule, Unassign, ResetAll)
///
/// Auto-schedule motoruyla karıştırılmamalı (algoritma AutoScheduleEngine'de).
/// </summary>
public partial class FacultyPlanningService : IFacultyPlanningService
{
    private readonly AppDbContext _context;
    private readonly ISchedulingService _schedulingService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AutoScheduleEngine _autoScheduleEngine;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public FacultyPlanningService(AppDbContext context, ISchedulingService schedulingService, IHttpContextAccessor httpContextAccessor, AutoScheduleEngine autoScheduleEngine)
    {
        _context = context;
        _schedulingService = schedulingService;
        _httpContextAccessor = httpContextAccessor;
        _autoScheduleEngine = autoScheduleEngine;
    }

    public async Task<int> ResolveFacultyForUserAsync(string userId, CancellationToken ct = default)
    {
        if (!int.TryParse(userId, out var userIdInt))
            throw new UnauthorizedAccessException("Gecersiz kullanici kimlik bilgisi.");

        var facultyId = await _context.Instructors
            .AsNoTracking()
            .Where(i => i.UserId == userIdInt)
            .Select(i => i.Department.FacultyId)
            .FirstOrDefaultAsync(ct);

        if (facultyId == 0)
            throw new UnauthorizedAccessException("Kullanici icin fakulte bilgisi bulunamadi.");

        return facultyId;
    }

    private async Task<int?> ResolveDepartmentForUserAsync(string userId, CancellationToken ct)
    {
        if (!int.TryParse(userId, out var userIdInt)) return null;

        return await _context.Instructors
            .AsNoTracking()
            .Where(i => i.UserId == userIdInt)
            .Select(i => (int?)i.DepartmentId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<FacultyPlanningOverviewDto> GetOverviewAsync(int facultyId, int termId, int? scopeDepartmentId = null, CancellationToken ct = default)
    {
        var faculty = await _context.Faculties
            .AsNoTracking()
            .Include(f => f.Departments)
            .FirstOrDefaultAsync(f => f.Id == facultyId, ct)
            ?? throw new KeyNotFoundException("Fakulte bulunamadi.");

        var term = await _context.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new KeyNotFoundException("Donem bulunamadi.");

        var departmentIds = scopeDepartmentId.HasValue
            ? new List<int> { scopeDepartmentId.Value }
            : faculty.Departments.Select(d => d.Id).ToList();

        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Classroom)
            .Where(cs => cs.TermId == termId && cs.Status == CourseSectionStatus.Active && departmentIds.Contains(cs.Course.DepartmentId ?? 0))
            .ToListAsync(ct);

        // Determine which sections have SectionSchedule records
        var overviewSectionIds = sections.Select(s => s.Id).ToList();
        var scheduledSectionIds = await _context.SectionSchedules
            .AsNoTracking()
            .Where(ss => overviewSectionIds.Contains(ss.CourseSectionId))
            .Select(ss => ss.CourseSectionId)
            .Distinct()
            .ToListAsync(ct);
        var scheduledSet = scheduledSectionIds.ToHashSet();

        var scheduled = sections.Count(s => s.ClassroomId.HasValue && scheduledSet.Contains(s.Id));
        var unscheduled = sections.Count - scheduled;

        var conflicts = await _schedulingService.GetConflictsAsync(termId, ct);
        var facultyConflicts = conflicts.Where(c =>
            sections.Any(s => s.Id == c.SectionId || s.Id == c.RelatedSectionId)).ToList();

        var conflictByType = facultyConflicts
            .GroupBy(c => c.ConflictType)
            .ToDictionary(g => g.Key, g => g.Count());

        var departmentProgress = new List<DepartmentProgressDto>();
        var visibleDepartments = scopeDepartmentId.HasValue
            ? faculty.Departments.Where(d => d.Id == scopeDepartmentId.Value)
            : faculty.Departments;
        foreach (var dept in visibleDepartments)
        {
            var deptSections = sections.Where(s => s.Course.DepartmentId == dept.Id).ToList();

            departmentProgress.Add(new DepartmentProgressDto
            {
                DepartmentId = dept.Id,
                DepartmentName = dept.Name,
                DepartmentCode = dept.Code,
                TotalSections = deptSections.Count,
                ScheduledSections = deptSections.Count(s => s.ClassroomId.HasValue && scheduledSet.Contains(s.Id)),
                PendingApprovals = 0
            });
        }

        var pendingWarnings = new List<PendingApprovalWarningDto>();

        var recentLogs = await _context.SchedulingAuditLogs
            .AsNoTracking()
            .Include(l => l.Section).ThenInclude(s => s.Course)
            .Where(l => sections.Select(s => s.Id).Contains(l.SectionId))
            .OrderByDescending(l => l.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        var userIdInts = recentLogs
            .Select(l => int.TryParse(l.UserId, out var uid) ? uid : 0)
            .Where(id => id > 0).Distinct().ToList();
        var userNameMap = await _context.Users
            .AsNoTracking()
            .Where(u => userIdInts.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName ?? "Bilinmeyen", ct);

        var classroomIds = recentLogs.Where(l => l.ClassroomId.HasValue).Select(l => l.ClassroomId!.Value).Distinct().ToList();
        var classroomNameMap = await _context.Classrooms
            .AsNoTracking()
            .Where(c => classroomIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        // Load SectionSchedule for recent log sections
        var recentLogSectionIds = recentLogs.Select(l => l.SectionId).Distinct().ToList();
        var recentLogSchedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => recentLogSectionIds.Contains(ss.CourseSectionId))
            .ToListAsync(ct);
        var recentLogScheduleMap = recentLogSchedules
            .GroupBy(ss => ss.CourseSectionId)
            .ToDictionary(g => g.Key, g => ScheduleHelper.FormatScheduleSummary(g));

        return new FacultyPlanningOverviewDto
        {
            FacultyId = faculty.Id,
            FacultyName = faculty.Name,
            TermId = term.Id,
            TermName = term.Name,
            TotalSections = sections.Count,
            ScheduledSections = scheduled,
            UnscheduledSections = unscheduled,
            ConflictCount = facultyConflicts.Count,
            ConflictCountByType = conflictByType,
            DepartmentProgress = departmentProgress,
            PendingApprovalWarnings = pendingWarnings,
            RecentAssignments = recentLogs.Select(l => new RecentAssignmentDto
            {
                SectionId = l.SectionId,
                CourseCode = l.Section.Course.Code,
                CourseName = l.Section.Course.Name,
                ClassroomName = l.ClassroomId.HasValue
                    ? classroomNameMap.GetValueOrDefault(l.ClassroomId.Value)
                    : null,
                ScheduleSummary = recentLogScheduleMap.GetValueOrDefault(l.SectionId, "Zamanlanmamis"),
                AssignedByUserName = int.TryParse(l.UserId, out var uid) ? userNameMap.GetValueOrDefault(uid, "Bilinmeyen") : "Bilinmeyen",
                Action = l.Action,
                AssignedAt = l.CreatedAt
            }).ToList()
        };
    }

    public async Task<FacultyPlanningSectionPageDto> GetSectionsAsync(
        int facultyId, int termId, int? departmentId = null, string? status = null,
        int page = 1, int pageSize = 50, string? search = null, CancellationToken ct = default)
    {
        var departmentIds = await _context.Departments
            .AsNoTracking()
            .Where(d => d.FacultyId == facultyId)
            .Select(d => d.Id)
            .ToListAsync(ct);

        if (departmentId.HasValue && !departmentIds.Contains(departmentId.Value))
            throw new UnauthorizedAccessException("Bu bolum belirtilen fakulteye ait degil.");

        var query = _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course).ThenInclude(c => c.Department)
            .Include(cs => cs.Classroom)
            .Include(cs => cs.Instructor)
            .Where(cs => cs.TermId == termId && cs.Status == CourseSectionStatus.Active)
            .Where(cs => departmentIds.Contains(cs.Course.DepartmentId ?? 0));

        if (departmentId.HasValue)
            query = query.Where(cs => cs.Course.DepartmentId == departmentId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(cs =>
                cs.Course.Code.ToLower().Contains(q) ||
                cs.Course.Name.ToLower().Contains(q));
        }

        var allSections = await query.OrderBy(cs => cs.Course.Code).ThenBy(cs => cs.Id).ToListAsync(ct);

        var grouped = allSections.GroupBy(s => new { s.CourseId, s.TermId });
        var items = new List<FacultyPlanningSectionDto>();

        var conflicts = await _schedulingService.GetConflictsAsync(termId, ct);
        var conflictSectionIds = conflicts.SelectMany(c => new[] { c.SectionId, c.RelatedSectionId ?? 0 }).ToHashSet();

        var instructorIds = allSections.Where(s => s.InstructorId.HasValue).Select(s => s.InstructorId!.Value).Distinct();
        var instructorNameMap = await LoadInstructorNameMapAsync(instructorIds, ct);

        // Load SectionSchedule records for schedule display
        var sectionPageIds = allSections.Select(s => s.Id).ToList();
        var sectionPageSchedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => sectionPageIds.Contains(ss.CourseSectionId))
            .ToListAsync(ct);
        var sectionPageScheduleMap = sectionPageSchedules
            .GroupBy(ss => ss.CourseSectionId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var sectionPageHasSchedule = sectionPageScheduleMap.Keys.ToHashSet();

        // Load SuggestedSemester from CurriculumCourse for year-level coloring
        var courseIds = allSections.Select(s => s.CourseId).Distinct().ToList();
        var semesterMap = await _context.Set<CurriculumCourse>()
            .AsNoTracking()
            .Where(cc => courseIds.Contains(cc.CourseId))
            .GroupBy(cc => cc.CourseId)
            .Select(g => new { CourseId = g.Key, Semester = g.Min(cc => cc.SuggestedSemester) })
            .ToDictionaryAsync(x => x.CourseId, x => x.Semester, ct);

        foreach (var group in grouped)
        {
            var index = 0;
            foreach (var s in group.OrderBy(x => x.Id))
            {
                index++;
                var hasClassroom = s.ClassroomId.HasValue;
                var hasSchedule = sectionPageHasSchedule.Contains(s.Id);
                var hasInstructor = s.InstructorId.HasValue;
                var hasConflict = conflictSectionIds.Contains(s.Id);

                // Online ders → ClassroomId her zaman null (sınıf yok); schedule varsa
                // "Scheduled" sayılmalı. Aksi halde online dersler hep "Unscheduled" gözüküyordu.
                var isOnlineCourse = s.Course.TeachingMethod == TeachingMethod.Online;
                string sectionStatus;
                if (hasConflict) sectionStatus = "Conflicted";
                else if (hasSchedule && (hasClassroom || isOnlineCourse)) sectionStatus = "Scheduled";
                else if (hasClassroom || hasInstructor) sectionStatus = "Partial";
                else sectionStatus = "Unscheduled";

                if (status != null && !string.Equals(sectionStatus, status, StringComparison.OrdinalIgnoreCase))
                    continue;

                var sectionScheds = sectionPageScheduleMap.GetValueOrDefault(s.Id)
                    ?? new List<SectionSchedule>();

                items.Add(new FacultyPlanningSectionDto
                {
                    SectionId = s.Id,
                    CourseId = s.CourseId,
                    CourseCode = s.Course.Code,
                    CourseName = s.Course.Name,
                    DepartmentId = s.Course.DepartmentId ?? 0,
                    DepartmentCode = s.Course.Department?.Code ?? "",
                    Capacity = s.Capacity,
                    ClassroomId = s.ClassroomId,
                    ClassroomName = s.Classroom?.Name,
                    ClassroomType = s.Classroom?.Type.ToString(),
                    InstructorId = s.InstructorId,
                    InstructorName = s.InstructorId.HasValue
                        ? instructorNameMap.GetValueOrDefault(s.InstructorId.Value)
                        : null,
                    ScheduleSummary = ScheduleHelper.FormatScheduleSummary(sectionScheds),
                    Status = sectionStatus,
                    TheoryHours = s.Course.TheoryHours,
                    LabHours = s.Course.LabHours,
                    SectionIndex = index,
                    IsPinned = s.IsPinned,
                    SuggestedSemester = semesterMap.GetValueOrDefault(s.CourseId, 0),
                    TeachingMethod = s.Course.TeachingMethod,
                });
            }
        }

        var totalCount = items.Count;
        var paged = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new FacultyPlanningSectionPageDto
        {
            Items = paged,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }


    public async Task<List<AvailableClassroomDto>> GetAvailableClassroomsAsync(
        int termId, DayOfWeek? day = null, TimeOnly? startTime = null, TimeOnly? endTime = null,
        int? minCapacity = null, string? type = null, CancellationToken ct = default)
    {
        var classrooms = await _context.Classrooms.AsNoTracking().ToListAsync(ct);

        if (minCapacity.HasValue)
            classrooms = classrooms.Where(c => c.Capacity >= minCapacity.Value).ToList();

        if (!string.IsNullOrEmpty(type) && Enum.TryParse<ClassroomType>(type, true, out var parsedType))
            classrooms = classrooms.Where(c => c.Type == parsedType).ToList();

        if (day.HasValue && startTime.HasValue && endTime.HasValue)
        {
            var occupiedClassroomIds = await GetOccupiedClassroomIdsAsync(termId, day.Value, startTime.Value, endTime.Value, ct);
            classrooms = classrooms.Where(c => !occupiedClassroomIds.Contains(c.Id)).ToList();
        }

        return classrooms
            .OrderBy(c => c.Capacity)
            .Select(c => new AvailableClassroomDto
            {
                ClassroomId = c.Id,
                Name = c.Name,
                Capacity = c.Capacity,
                Type = c.Type.ToString(),
                FitScore = minCapacity.HasValue
                    ? Math.Max(0, 1m - Math.Abs(c.Capacity - minCapacity.Value) / (decimal)Math.Max(c.Capacity, 1))
                    : 1m
            })
            .OrderByDescending(c => c.FitScore)
            .ToList();
    }

    public async Task<AutoSchedulePreviewDto> AutoSchedulePreviewAsync(AutoScheduleRequest request, string userId, CancellationToken ct = default)
    {
        // userId'yi RunAsync'e geçir → IScheduleProgressReporter SignalR ile o kullanıcıya
        // canlı stage event'leri yayınlasın (started → construction → sa → lns → complete).
        var result = await _autoScheduleEngine.RunAsync(request, userId, ct);

        // Save run to DB
        var run = new ScheduleRun
        {
            FacultyId = request.FacultyId,
            TermId = request.TermId,
            Seed = result.Seed,
            TotalProcessed = result.TotalProcessed,
            SuccessCount = result.SuccessCount,
            FailedCount = result.FailedCount,
            TotalPenalty = result.TotalPenalty,
            ElapsedSeconds = result.Stats?.ElapsedSeconds ?? 0,
            ResultJson = JsonSerializer.Serialize(result, JsonOptions),
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = int.TryParse(userId, out var uid) ? uid : null
        };
        _context.ScheduleRuns.Add(run);
        await _context.SaveChangesAsync(ct);

        return result;
    }

    public async Task<List<ScheduleRunSummaryDto>> GetScheduleRunsAsync(int facultyId, int termId, CancellationToken ct = default)
    {
        return await _context.ScheduleRuns
            .AsNoTracking()
            .Where(r => r.FacultyId == facultyId && r.TermId == termId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .Select(r => new ScheduleRunSummaryDto
            {
                Id = r.Id,
                Seed = r.Seed,
                TotalProcessed = r.TotalProcessed,
                SuccessCount = r.SuccessCount,
                FailedCount = r.FailedCount,
                TotalPenalty = r.TotalPenalty,
                ElapsedSeconds = r.ElapsedSeconds,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<ScheduleRunDetailDto?> GetScheduleRunAsync(int id, CancellationToken ct = default)
    {
        var run = await _context.ScheduleRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (run == null) return null;

        var result = JsonSerializer.Deserialize<AutoSchedulePreviewDto>(run.ResultJson, JsonOptions);

        return new ScheduleRunDetailDto
        {
            Id = run.Id,
            Seed = run.Seed,
            CreatedAt = run.CreatedAt,
            Result = result!
        };
    }


    public async Task<List<SchedulingConflictDto>> GetConflictsAsync(int facultyId, int termId, CancellationToken ct = default)
    {
        var departmentIds = await _context.Departments
            .AsNoTracking()
            .Where(d => d.FacultyId == facultyId)
            .Select(d => d.Id)
            .ToListAsync(ct);

        var allConflicts = await _schedulingService.GetConflictsAsync(termId, ct);

        var facultySectionIds = (await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.TermId == termId && cs.Status == CourseSectionStatus.Active && departmentIds.Contains(cs.Course.DepartmentId ?? 0))
            .Select(cs => cs.Id)
            .ToListAsync(ct)).ToHashSet();

        return allConflicts
            .Where(c => facultySectionIds.Contains(c.SectionId) || (c.RelatedSectionId.HasValue && facultySectionIds.Contains(c.RelatedSectionId.Value)))
            .ToList();
    }

    private async Task<Dictionary<int, string>> LoadInstructorNameMapAsync(IEnumerable<int> instructorIds, CancellationToken ct)
    {
        var ids = instructorIds.Distinct().ToList();
        if (ids.Count == 0) return new();

        var instructors = await _context.Instructors
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .Select(i => new { i.Id, i.Title, i.UserId })
            .ToListAsync(ct);

        var userIds = instructors.Select(i => i.UserId).Distinct().ToList();
        var userNameMap = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName ?? "", ct);

        return instructors.ToDictionary(
            i => i.Id,
            i => userNameMap.TryGetValue(i.UserId, out var name) ? $"{i.Title} {name}" : $"{i.Title} (#{i.Id})");
    }

    private async Task<HashSet<int>> GetOccupiedClassroomIdsAsync(
        int termId, DayOfWeek day, TimeOnly startTime, TimeOnly endTime, CancellationToken ct)
    {
        // CourseSection.IsActive bir computed property (`=> Status == Active`) ve EF Core
        // bunu SQL'e çeviremiyor — sorgu runtime'da InvalidOperationException atıyor ve
        // frontend "uygun derslik yok" görüyor. Direkt Status karşılaştırması kullan.
        var occupiedClassroomIds = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Include(ss => ss.CourseSection)
            .Where(ss => ss.CourseSection.TermId == termId
                && ss.CourseSection.Status == Domain.Enums.CourseSectionStatus.Active
                && ss.TimeSlot.DayOfWeek == day
                && ss.TimeSlot.StartTime < endTime
                && ss.TimeSlot.EndTime > startTime)
            .Where(ss => ss.ClassroomId != null)
            .Select(ss => ss.ClassroomId!.Value)
            .Distinct()
            .ToListAsync(ct);

        return occupiedClassroomIds.ToHashSet();
    }

    private async Task ClearSectionScheduleRecordsAsync(int sectionId, CancellationToken ct)
    {
        var existingRecords = await _context.SectionSchedules
            .Where(ss => ss.CourseSectionId == sectionId)
            .ToListAsync(ct);

        if (existingRecords.Count > 0)
        {
            _context.SectionSchedules.RemoveRange(existingRecords);
        }
    }

    private async Task WriteSectionScheduleRecordsAsync(
        int sectionId,
        int termId,
        int? classroomId,
        IReadOnlyList<ScheduleSlotDto> scheduleSlots,
        CancellationToken ct)
    {
        await ClearSectionScheduleRecordsAsync(sectionId, ct);

        if (scheduleSlots.Count == 0)
            return;

        var termTimeSlots = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .ToListAsync(ct);

        // Resolve which academic time slots are covered by the requested ranges
        var allMatchedSlots = new List<(TimeSlot timeSlot, int? classroomId)>();

        foreach (var slot in scheduleSlots)
        {
            var matchingTimeSlots = termTimeSlots
                .Where(ts => ts.DayOfWeek == slot.Day
                    && ts.StartTime < slot.EndTime
                    && slot.StartTime < ts.EndTime)
                .OrderBy(ts => ts.SlotOrder)
                .ToList();

            if (matchingTimeSlots.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{DayLabel(slot.Day)} {slot.StartTime:HH\\:mm}-{slot.EndTime:HH\\:mm} araligi donem zaman slotlari ile eslesmiyor.");
            }

            // Online section için slot.ClassroomId ve classroomId ikisi de null olabilir — bu kabul
            var effectiveClassroomId = slot.ClassroomId ?? classroomId;
            foreach (var ts in matchingTimeSlots)
                allMatchedSlots.Add((ts, effectiveClassroomId));
        }

        // Validate total slot count matches course hours
        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .FirstAsync(cs => cs.Id == sectionId, ct);
        var expectedSlotCount = section.Course.TheoryHours + section.Course.LabHours;
        if (expectedSlotCount > 0 && allMatchedSlots.Count != expectedSlotCount)
        {
            throw new InvalidOperationException(
                $"Slot sayisi uyumsuz: {section.Course.Code} icin {expectedSlotCount} slot gerekli, {allMatchedSlots.Count} slot secildi.");
        }

        var enforceClassroomConflict = await IsConstraintEnabledAsHardAsync("H1", defaultIsHard: true, ct);

        // Write with hard guard against classroom double-booking (online section'lar: ClassroomId null → çakışma kontrolü yok)
        foreach (var (timeSlot, effectiveCrId) in allMatchedSlots)
        {
            if (enforceClassroomConflict && effectiveCrId.HasValue)
            {
                var alreadyOccupied = await _context.SectionSchedules
                    .AnyAsync(ss => ss.ClassroomId == effectiveCrId.Value
                        && ss.TimeSlotId == timeSlot.Id
                        && ss.CourseSectionId != sectionId, ct);

                if (alreadyOccupied)
                {
                    throw new InvalidOperationException(
                        $"Derslik cakismasi: {DayLabel(timeSlot.DayOfWeek)} {timeSlot.StartTime:HH\\:mm}-{timeSlot.EndTime:HH\\:mm} slotunda derslik zaten dolu.");
                }
            }

            _context.SectionSchedules.Add(new SectionSchedule
            {
                CourseSectionId = sectionId,
                TimeSlotId = timeSlot.Id,
                ClassroomId = effectiveCrId
            });
        }
    }

    private async Task<bool> IsConstraintEnabledAsHardAsync(string code, bool defaultIsHard, CancellationToken ct)
    {
        var definition = await _context.ConstraintDefinitions
            .AsNoTracking()
            .Where(d => d.Code == code)
            .Select(d => new { d.IsEnabled, d.Category })
            .FirstOrDefaultAsync(ct);

        if (definition == null)
            return defaultIsHard;

        if (!definition.IsEnabled)
            return false;

        return string.Equals(definition.Category, "Hard", StringComparison.OrdinalIgnoreCase);
    }

    private static string DayLabel(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pzt",
        DayOfWeek.Tuesday => "Sal",
        DayOfWeek.Wednesday => "Car",
        DayOfWeek.Thursday => "Per",
        DayOfWeek.Friday => "Cum",
        _ => day.ToString()
    };

    private async Task<List<object>> GetCurrentScheduleSlotPayloadAsync(int sectionId, CancellationToken ct)
    {
        var schedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => ss.CourseSectionId == sectionId)
            .OrderBy(ss => ss.TimeSlot.DayOfWeek)
            .ThenBy(ss => ss.TimeSlot.StartTime)
            .ToListAsync(ct);

        return schedules
            .Select(ss => new
            {
                day = ss.TimeSlot.DayOfWeek.ToString(),
                startTime = ss.TimeSlot.StartTime.ToString("HH\\:mm"),
                endTime = ss.TimeSlot.EndTime.ToString("HH\\:mm"),
                classroomId = ss.ClassroomId,
            })
            .Cast<object>()
            .ToList();
    }

    private string? GetCurrentIpAddress() => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    private static int? TryParseUserId(string userId) => int.TryParse(userId, out var parsedUserId) ? parsedUserId : null;

    private static string GetTermStatusLabel(TermStatus s) => s switch
    {
        TermStatus.Inactive  => "Pasif",
        TermStatus.Active    => "Aktif",
        TermStatus.Cancelled => "İptal",
        _                    => s.ToString()
    };

    public async Task<List<FacultyPlanningTimeSlotDto>> GetTimeSlotsAsync(int termId, CancellationToken ct = default)
    {
        return await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .OrderBy(ts => ts.DayOfWeek)
            .ThenBy(ts => ts.SlotOrder)
            .Select(ts => new FacultyPlanningTimeSlotDto
            {
                Day = (int)ts.DayOfWeek,
                SlotOrder = ts.SlotOrder,
                StartTime = ts.StartTime,
                EndTime = ts.EndTime,
                DisplayName = ts.DisplayName,
                AllowedMode = ts.AllowedMode
            })
            .ToListAsync(ct);
    }

    public async Task<int?> ResolveFacultyIdForSectionAsync(int sectionId, CancellationToken ct = default)
    {
        return await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.Id == sectionId)
            .Select(cs => (int?)cs.Course!.Department!.FacultyId)
            .FirstOrDefaultAsync(ct);
    }
}
