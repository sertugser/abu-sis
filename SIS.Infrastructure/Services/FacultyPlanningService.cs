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

namespace SIS.Infrastructure.Services;

public class FacultyPlanningService : IFacultyPlanningService
{
    private readonly AppDbContext _context;
    private readonly ISchedulingService _schedulingService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public FacultyPlanningService(AppDbContext context, ISchedulingService schedulingService, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _schedulingService = schedulingService;
        _httpContextAccessor = httpContextAccessor;
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
            .Where(cs => cs.TermId == termId && cs.IsActive && departmentIds.Contains(cs.Course.DepartmentId ?? 0))
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
            .Where(cs => cs.TermId == termId && cs.IsActive)
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

                string sectionStatus;
                if (hasConflict) sectionStatus = "Conflicted";
                else if (hasClassroom && hasSchedule) sectionStatus = "Scheduled";
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
                    IsPinned = s.IsPinned
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

    public async Task<AssignmentResultDto> AssignSectionAsync(AssignSectionRequest request, string userId, CancellationToken ct = default)
    {
        // B14: wrap entire method in a single transaction so section update, schedule write,
        // and audit logs are all committed atomically. If we are already inside an outer
        // transaction (e.g. called from ApplyScheduleAsync) we reuse it instead of nesting.
        var ownsTransaction = _context.Database.CurrentTransaction == null;
        await using var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            var section = await _context.CourseSections
                .Include(cs => cs.Course)
                .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, ct)
                ?? throw new KeyNotFoundException("Sube bulunamadi.");

            var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == section.TermId, ct);
            if (term != null && term.Status != TermStatus.Planning && term.Status != TermStatus.Scheduling)
                throw new InvalidOperationException(
                    $"Hoca/derslik/zaman ataması için dönem 'Planlama' veya 'Programlama' aşamasında olmalıdır. Mevcut durum: {GetTermStatusLabel(term.Status)}");

            if (section.IsFinalized)
                throw new InvalidOperationException("Bu sube kesinlesmis, degisiklik yapilamaz.");

            var userDeptId = await ResolveDepartmentForUserAsync(userId, ct);
            if (userDeptId.HasValue && section.Course.DepartmentId != userDeptId.Value)
                throw new UnauthorizedAccessException("Sadece kendi bolumunuzun subelerine atama yapabilirsiniz.");

            var result = new AssignmentResultDto { Success = true };
            var previousClassroomId = section.ClassroomId;

            if (request.ClassroomId.HasValue)
            {
                if (request.ScheduleSlots == null || request.ScheduleSlots.Count == 0)
                    throw new ArgumentException("Derslik atanirken zaman slotlari da belirtilmelidir.");

                var classroom = await _context.Classrooms
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == request.ClassroomId.Value, ct)
                    ?? throw new KeyNotFoundException("Derslik bulunamadi.");

                if (classroom.Capacity < section.Capacity)
                    result.Warnings.Add($"Derslik kapasitesi ({classroom.Capacity}) sube kapasitesinden ({section.Capacity}) dusuk.");

                var expectedType = section.Course.LabHours > 0 && section.Course.TheoryHours == 0
                    ? ClassroomType.Lab : ClassroomType.Lecture;
                if (classroom.Type != expectedType)
                    result.Warnings.Add($"Derslik tipi uyumsuz: beklenen {expectedType}, atanan {classroom.Type}.");

                section.ClassroomId = request.ClassroomId.Value;
            }
            else if (request.ScheduleSlots is { Count: > 0 })
            {
                throw new ArgumentException("Zaman slotu atanirken derslik de belirtilmelidir.");
            }

            if (request.InstructorId.HasValue)
            {
                var instructorExists = await _context.Instructors.AnyAsync(i => i.Id == request.InstructorId.Value, ct);
                if (!instructorExists)
                    throw new KeyNotFoundException("Ogretim uyesi bulunamadi.");
                section.InstructorId = request.InstructorId.Value;

                // K1: Hoca günlük yük limiti kontrolü
                if (request.ScheduleSlots is { Count: > 0 })
                {
                    var instrId = request.InstructorId.Value;
                    var requestDays = request.ScheduleSlots.Select(s => s.Day).Distinct();
                    var existingInstructorSchedules = await _context.SectionSchedules
                        .AsNoTracking()
                        .Include(ss => ss.TimeSlot)
                        .Where(ss => ss.CourseSection.InstructorId == instrId
                            && ss.CourseSection.TermId == section.TermId
                            && ss.CourseSectionId != section.Id)
                        .ToListAsync(ct);

                    foreach (var day in requestDays)
                    {
                        var existingHours = existingInstructorSchedules
                            .Where(ss => ss.TimeSlot.DayOfWeek == day)
                            .Sum(ss => (ss.TimeSlot.EndTime - ss.TimeSlot.StartTime).TotalHours);
                        var newHours = request.ScheduleSlots
                            .Where(s => s.Day == day)
                            .Sum(s => (s.EndTime - s.StartTime).TotalHours);
                        if (existingHours + newHours > 9)
                            throw new InvalidOperationException(
                                $"Ogretim uyesinin {day} gunundeki toplam yuku {existingHours + newHours:F1} saate ulasacak (limit: 9 saat). Atama reddedildi.");
                    }
                }
            }

            // Write schedule records only if this is a NEW schedule assignment (not just an instructor change)
            // Skip if section already has schedule records and classroom hasn't changed
            var hasSlotLevelClassrooms = request.ScheduleSlots?.Any(s => s.ClassroomId.HasValue) == true;
            if ((request.ClassroomId.HasValue || hasSlotLevelClassrooms) && request.ScheduleSlots is { Count: > 0 })
            {
                var existingScheduleCount = await _context.SectionSchedules
                    .CountAsync(ss => ss.CourseSectionId == section.Id, ct);

                var classroomChanged = request.ClassroomId.HasValue && previousClassroomId != request.ClassroomId.Value;
                var isNewSchedule = existingScheduleCount == 0;

                if (isNewSchedule || classroomChanged)
                {
                    await WriteSectionScheduleRecordsAsync(
                        section.Id,
                        section.TermId,
                        request.ClassroomId ?? section.ClassroomId ?? 0,
                        request.ScheduleSlots,
                        ct);
                }
            }

            var conflicts = await _schedulingService.GetConflictsAsync(section.TermId, ct);
            var sectionConflicts = conflicts.Where(c => c.SectionId == section.Id || c.RelatedSectionId == section.Id).ToList();
            result.Conflicts = sectionConflicts;

            _context.SchedulingAuditLogs.Add(new SchedulingAuditLog
            {
                SectionId = section.Id,
                Action = "Assign",
                UserId = userId,
                ClassroomId = request.ClassroomId,
                InstructorId = request.InstructorId
            });
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = TryParseUserId(userId),
                Action = "ScheduleAssigned",
                EntityType = nameof(CourseSection),
                EntityId = section.Id.ToString(),
                NewValue = JsonSerializer.Serialize(new
                {
                    classroomId = request.ClassroomId ?? section.ClassroomId,
                    instructorId = request.InstructorId ?? section.InstructorId,
                    slots = request.ScheduleSlots?.Select(slot => new
                    {
                        day = slot.Day.ToString(),
                        startTime = slot.StartTime.ToString("HH\\:mm"),
                        endTime = slot.EndTime.ToString("HH\\:mm"),
                        classroomId = slot.ClassroomId ?? request.ClassroomId ?? section.ClassroomId,
                    }),
                }, JsonOptions),
                Timestamp = DateTime.UtcNow,
                IpAddress = GetCurrentIpAddress(),
            });

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("Bu sube baska bir kullanici tarafindan guncellendi. Lutfen yenileyip tekrar deneyin.");
            }

            if (ownsTransaction)
                await transaction!.CommitAsync(ct);

            return result;
        }
        catch
        {
            if (ownsTransaction && transaction != null)
                await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task UnassignSectionAsync(int sectionId, string userId, CancellationToken ct = default)
    {
        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == sectionId, ct)
            ?? throw new KeyNotFoundException("Sube bulunamadi.");

        var unassignTerm = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == section.TermId, ct);
        if (unassignTerm != null && unassignTerm.Status != TermStatus.Planning && unassignTerm.Status != TermStatus.Scheduling)
            throw new InvalidOperationException(
                $"Atama kaldırma sadece 'Planlama' veya 'Programlama' aşamasında yapılabilir. Mevcut durum: {GetTermStatusLabel(unassignTerm.Status)}");

        if (section.IsFinalized)
            throw new InvalidOperationException("Bu sube kesinlesmis, degisiklik yapilamaz.");

        var userDeptId = await ResolveDepartmentForUserAsync(userId, ct);
        if (userDeptId.HasValue && section.Course.DepartmentId != userDeptId.Value)
            throw new UnauthorizedAccessException("Sadece kendi bolumunuzun subelerinin atamasini kaldirabilirsiniz.");

        var previousClassroomId = section.ClassroomId;
        var previousInstructorId = section.InstructorId;
        var previousSlots = await GetCurrentScheduleSlotPayloadAsync(sectionId, ct);

        await ClearSectionScheduleRecordsAsync(sectionId, ct);
        section.ClassroomId = null;
        section.InstructorId = null;

        _context.SchedulingAuditLogs.Add(new SchedulingAuditLog
        {
            SectionId = sectionId,
            Action = "Unassign",
            UserId = userId
        });
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = TryParseUserId(userId),
            Action = "ScheduleUnassigned",
            EntityType = nameof(CourseSection),
            EntityId = sectionId.ToString(),
            NewValue = JsonSerializer.Serialize(new
            {
                previousClassroomId,
                previousInstructorId,
                slots = previousSlots,
                note = "Assignment removed",
            }, JsonOptions),
            Timestamp = DateTime.UtcNow,
            IpAddress = GetCurrentIpAddress(),
        });

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("Bu sube baska bir kullanici tarafindan guncellendi. Lutfen yenileyip tekrar deneyin.");
        }
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

    public async Task<AutoSchedulePreviewDto> AutoSchedulePreviewAsync(AutoScheduleRequest request, CancellationToken ct = default)
    {
        var result = new AutoSchedulePreviewDto();

        var departmentIds = await _context.Departments
            .AsNoTracking()
            .Where(d => d.FacultyId == request.FacultyId)
            .Select(d => d.Id)
            .ToListAsync(ct);

        // Find sections that have SectionSchedule records (i.e., are already scheduled)
        var termScheduledSectionIds = await _context.SectionSchedules
            .AsNoTracking()
            .Where(ss => ss.CourseSection.TermId == request.TermId)
            .Select(ss => ss.CourseSectionId)
            .Distinct()
            .ToListAsync(ct);
        var termScheduledSet = termScheduledSectionIds.ToHashSet();

        // Load candidate sections (unscheduled = no SectionSchedule records OR no classroom)
        var candidateQuery = _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course).ThenInclude(c => c.Department)
            .Include(cs => cs.Instructor)
            .Where(cs => cs.TermId == request.TermId && cs.IsActive && !cs.IsFinalized)
            .Where(cs => departmentIds.Contains(cs.Course.DepartmentId ?? 0));

        var query = candidateQuery;

        if (request.DepartmentId.HasValue)
            query = query.Where(cs => cs.Course.DepartmentId == request.DepartmentId.Value);

        if (request.SectionIds is { Count: > 0 })
            query = query.Where(cs => request.SectionIds.Contains(cs.Id));

        var allCandidateSections = await query.ToListAsync(ct);
        // Filter to only unscheduled sections (no SectionSchedule records or no classroom)
        // Pinned sections are excluded — they have fixed schedules set externally
        var sections = allCandidateSections
            .Where(s => !s.IsPinned && (!s.ClassroomId.HasValue || !termScheduledSet.Contains(s.Id)))
            .ToList();

        var courseIds = sections.Select(s => s.CourseId).Distinct().ToList();
        var compulsoryCourseIds = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => courseIds.Contains(cc.CourseId) && cc.IsCompulsory)
            .Where(cc => cc.Curriculum.IsActive)
            .Select(cc => cc.CourseId)
            .Distinct()
            .ToListAsync(ct);

        var allCurriculumCourses = await _context.CurriculumCourses
            .AsNoTracking()
            .Include(cc => cc.Curriculum)
            .ThenInclude(c => c.Program)
            .Where(cc => cc.Curriculum.IsActive && cc.IsCompulsory)
            .Select(cc => new { cc.CourseId, cc.Curriculum.Program.DepartmentId, cc.SuggestedSemester })
            .ToListAsync(ct);

        var curriculumSemesterMap = allCurriculumCourses
            .GroupBy(cc => cc.CourseId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(cc => (cc.DepartmentId, cc.SuggestedSemester)).Distinct().ToList());

        // Exclude non-schedulable courses: internships (490), graduation projects (491, 492)
        // These are mentor-supervised, not held in classrooms
        sections = sections
            .Where(s => !IsNonSchedulableCourse(s.Course.Code))
            .OrderByDescending(s => s.Course.LabHours > 0 ? 1 : 0)
            .ThenByDescending(s => compulsoryCourseIds.Contains(s.CourseId) ? 1 : 0)
            .ThenByDescending(s => s.Capacity)
            .ToList();

        var classrooms = await _context.Classrooms.AsNoTracking().ToListAsync(ct);
        // Load all scheduled sections' SectionSchedule records for conflict checking
        var allScheduledRecords = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Include(ss => ss.CourseSection)
            .Where(ss => ss.CourseSection.TermId == request.TermId && ss.CourseSection.IsActive)
            .ToListAsync(ct);

        var termTimeSlots = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == request.TermId)
            .OrderBy(ts => ts.DayOfWeek).ThenBy(ts => ts.SlotOrder)
            .ToListAsync(ct);

        var timeSlotsByDay = termTimeSlots
            .GroupBy(ts => ts.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.OrderBy(ts => ts.SlotOrder).ToList());

        var scheduledSlotsByClassroom = allScheduledRecords
            .GroupBy(ss => ss.ClassroomId)
            .ToDictionary(g => g.Key, g => g.Select(ss => new ScheduleSlotSimple(
                ss.TimeSlot.DayOfWeek, ss.TimeSlot.StartTime, ss.TimeSlot.EndTime)).ToList());

        var sectionInstructorIds = sections.Where(s => s.InstructorId.HasValue).Select(s => s.InstructorId!.Value).Distinct();
        var instructorNameMap = await LoadInstructorNameMapAsync(sectionInstructorIds, ct);

        // Build instructor schedule map for instructor conflict + daily load + day-off checks
        var scheduledSlotsByInstructor = allScheduledRecords
            .Where(ss => ss.CourseSection.InstructorId.HasValue)
            .GroupBy(ss => ss.CourseSection.InstructorId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(ss => new ScheduleSlotSimple(
                ss.TimeSlot.DayOfWeek, ss.TimeSlot.StartTime, ss.TimeSlot.EndTime)).ToList());

        var previewAssignments = new List<(int sectionId, int courseId, int classroomId, DayOfWeek day, TimeOnly start, TimeOnly end)>();
        var previewInstructorAssignments = new List<(int instructorId, DayOfWeek day, TimeOnly start, TimeOnly end)>();

        foreach (var section in sections)
        {
            result.TotalProcessed++;

            var requiredType = section.Course.LabHours > 0 && section.Course.TheoryHours == 0
                ? ClassroomType.Lab : ClassroomType.Lecture;

            var candidateClassrooms = classrooms
                .Where(c => c.Capacity >= section.Capacity)
                .OrderByDescending(c => IsCompatibleClassroomType(c.Type, requiredType) ? 1 : 0)
                .ThenBy(c => c.Capacity - section.Capacity)
                .ToList();

            if (candidateClassrooms.Count == 0)
            {
                result.Failures.Add(new AutoScheduleFailureDto
                {
                    SectionId = section.Id,
                    CourseCode = section.Course.Code,
                    CourseName = section.Course.Name,
                    Reason = $"Uygun kapasitede derslik bulunamadi (gerekli: {section.Capacity})."
                });
                result.FailedCount++;
                continue;
            }

            var totalHours = section.Course.TheoryHours + section.Course.LabHours;
            if (totalHours <= 0) totalHours = 3;

            // Split into two blocks if theory+lab can't fit in one consecutive window
            // Both blocks use the SAME classroom to avoid per-slot ClassroomId issues
            var needsSplit = section.Course.TheoryHours > 0 && section.Course.LabHours > 0;
            var blockSizes = needsSplit
                ? new[] { section.Course.TheoryHours, section.Course.LabHours }
                : new[] { totalHours };

            // ── Find best classroom + time slot(s) for ALL blocks ──
            var bestResult = FindBestAssignment(
                section, candidateClassrooms, timeSlotsByDay, blockSizes, requiredType,
                scheduledSlotsByClassroom, previewAssignments,
                scheduledSlotsByInstructor, previewInstructorAssignments,
                allScheduledRecords, curriculumSemesterMap);

            if (bestResult != null)
            {
                var fitScore = NormalizeAutoScheduleScore(bestResult.TotalScore / blockSizes.Length);
                var scheduleSummary = string.Join(" + ", bestResult.Slots.Select(s => $"{DayLabel(s.Day)} {s.StartTime:HH:mm}-{s.EndTime:HH:mm}"));

                result.Assignments.Add(new AutoSchedulePreviewItemDto
                {
                    SectionId = section.Id,
                    CourseCode = section.Course.Code,
                    CourseName = section.Course.Name,
                    DepartmentCode = section.Course.Department?.Code ?? "",
                    ClassroomId = bestResult.ClassroomId,
                    ClassroomName = bestResult.ClassroomName,
                    ScheduleSlots = bestResult.Slots,
                    ScheduleSummary = scheduleSummary,
                    InstructorId = section.InstructorId,
                    InstructorName = section.InstructorId.HasValue
                        ? instructorNameMap.GetValueOrDefault(section.InstructorId.Value) : null,
                    FitScore = fitScore
                });

                // Commit ALL blocks to preview tracking
                foreach (var slot in bestResult.Slots)
                    previewAssignments.Add((section.Id, section.CourseId, bestResult.ClassroomId, slot.Day, slot.StartTime, slot.EndTime));
                if (section.InstructorId.HasValue)
                    foreach (var slot in bestResult.Slots)
                        previewInstructorAssignments.Add((section.InstructorId.Value, slot.Day, slot.StartTime, slot.EndTime));

                result.SuccessCount++;
            }
            else
            {
                result.Failures.Add(new AutoScheduleFailureDto
                {
                    SectionId = section.Id,
                    CourseCode = section.Course.Code,
                    CourseName = section.Course.Name,
                    Reason = "Uygun zaman slotu bulunamadi (tum derslikler/saatler dolu veya kisitlamalar nedeniyle)."
                });
                result.FailedCount++;
            }
        }

        return result;
    }

    public async Task<ApplyScheduleResultDto> ApplyScheduleAsync(ApplyScheduleRequest request, string userId, CancellationToken ct = default)
    {
        var result = new ApplyScheduleResultDto();

        // B15: wrap all assignments in a single transaction so that a failure in any
        // assignment rolls back all previously applied assignments atomically.
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var item in request.Assignments)
            {
                // AssignSectionAsync detects the active transaction and reuses it
                // instead of opening a nested one.
                var assignResult = await AssignSectionAsync(new AssignSectionRequest
                {
                    SectionId = item.SectionId,
                    ClassroomId = item.ClassroomId,
                    ScheduleSlots = item.ScheduleSlots,
                    InstructorId = item.InstructorId
                }, userId, ct);

                if (assignResult.Success) result.SuccessCount++;
                else
                {
                    result.FailedCount++;
                    result.Errors.Add($"Sube {item.SectionId}: Atama basarisiz.");
                }
            }

            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            result.RolledBack = true;
            result.FailedCount++;
            result.Errors.Add($"Tum atamalar geri alindi: {ex.Message}");
        }

        return result;
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
            .Where(cs => cs.TermId == termId && cs.IsActive && departmentIds.Contains(cs.Course.DepartmentId ?? 0))
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
        var occupiedClassroomIds = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Include(ss => ss.CourseSection)
            .Where(ss => ss.CourseSection.TermId == termId
                && ss.CourseSection.IsActive
                && ss.TimeSlot.DayOfWeek == day
                && ss.TimeSlot.StartTime < endTime
                && ss.TimeSlot.EndTime > startTime)
            .Select(ss => ss.ClassroomId)
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
        int classroomId,
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
        var allMatchedSlots = new List<(TimeSlot timeSlot, int classroomId)>();

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

        // Write with hard guard against classroom double-booking
        foreach (var (timeSlot, effectiveCrId) in allMatchedSlots)
        {
            var alreadyOccupied = await _context.SectionSchedules
                .AnyAsync(ss => ss.ClassroomId == effectiveCrId
                    && ss.TimeSlotId == timeSlot.Id
                    && ss.CourseSectionId != sectionId, ct);

            if (alreadyOccupied)
            {
                throw new InvalidOperationException(
                    $"Derslik cakismasi: {DayLabel(timeSlot.DayOfWeek)} {timeSlot.StartTime:HH\\:mm}-{timeSlot.EndTime:HH\\:mm} slotunda derslik zaten dolu.");
            }

            _context.SectionSchedules.Add(new SectionSchedule
            {
                CourseSectionId = sectionId,
                TimeSlotId = timeSlot.Id,
                ClassroomId = effectiveCrId
            });
        }
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

    private static double CalculateAutoScheduleScore(
        CourseSection section,
        Classroom classroom,
        ClassroomType requiredType,
        DayOfWeek day,
        int startSlotOrder,
        int totalHours,
        IReadOnlyDictionary<int, List<ScheduleSlotSimple>> scheduledSlotsByInstructor,
        IReadOnlyList<(int instructorId, DayOfWeek day, TimeOnly start, TimeOnly end)> previewInstructorAssignments,
        IReadOnlyList<(int sectionId, int courseId, int classroomId, DayOfWeek day, TimeOnly start, TimeOnly end)> previewAssignments)
    {
        double score = 100;

        if (IsCompatibleClassroomType(classroom.Type, requiredType)) score += 20;

        var capacityOverage = classroom.Capacity - section.Capacity;
        score += Math.Max(0, 15 - capacityOverage * 0.3);

        if (section.InstructorId.HasValue)
        {
            var instrId = section.InstructorId.Value;
            var usedDays = new HashSet<DayOfWeek>();

            if (scheduledSlotsByInstructor.TryGetValue(instrId, out var instructorSlots))
            {
                foreach (var slot in instructorSlots) usedDays.Add(slot.Day);
            }

            foreach (var preview in previewInstructorAssignments.Where(p => p.instructorId == instrId))
            {
                usedDays.Add(preview.day);
            }

            if (!usedDays.Contains(day))
            {
                if (usedDays.Count >= 4) score -= 50;
            }
            else
            {
                score += 10;
            }

            var dailyHours = (scheduledSlotsByInstructor.TryGetValue(instrId, out var dailySlots)
                ? dailySlots.Where(s => s.Day == day).Sum(s => (s.EndTime - s.StartTime).TotalHours)
                : 0)
                + previewInstructorAssignments.Where(p => p.instructorId == instrId && p.day == day)
                    .Sum(p => (p.end - p.start).TotalHours);

            var projectedDaily = dailyHours + totalHours;
            if (projectedDaily > 6) score -= (projectedDaily - 6) * 10;
        }

        // Time preference: all daytime slots equal, only evening penalized
        if (startSlotOrder >= 10) score -= 10; // evening penalty

        // Spread across days: small penalty for days that already have many assignments
        var daySectionCount = previewAssignments.Count(p => p.day == day);
        if (daySectionCount > 15) score -= (daySectionCount - 15) * 2; // only penalize after 15 per day

        // Spread across classrooms: penalize overloaded classrooms
        var classroomUsageCount = previewAssignments.Count(p => p.classroomId == classroom.Id);
        if (classroomUsageCount > 8) score -= (classroomUsageCount - 8); // only after 8 uses

        return score;
    }

    private static bool HasCurriculumConflict(
        CourseSection section,
        DayOfWeek day, TimeOnly start, TimeOnly end,
        List<(int sectionId, int courseId, int classroomId, DayOfWeek day, TimeOnly start, TimeOnly end)> previewAssignments,
        List<SectionSchedule> existingScheduleRecords,
        Dictionary<int, List<(int DepartmentId, int SuggestedSemester)>> curriculumSemesterMap)
    {
        if (!curriculumSemesterMap.ContainsKey(section.CourseId))
            return false;

        // Check against preview assignments in this batch
        foreach (var p in previewAssignments)
        {
            if (p.day != day) continue;
            if (p.start >= end || p.end <= start) continue;
            if (p.courseId == section.CourseId) continue;
            if (SharesSameYearCurriculumAnyDepartment(section.CourseId, p.courseId, curriculumSemesterMap))
                return true;
        }

        // Check against existing scheduled sections (from DB)
        foreach (var record in existingScheduleRecords)
        {
            if (record.TimeSlot.DayOfWeek != day) continue;
            if (record.TimeSlot.StartTime >= end || record.TimeSlot.EndTime <= start) continue;
            if (record.CourseSection.CourseId == section.CourseId) continue;
            if (SharesSameYearCurriculumAnyDepartment(section.CourseId, record.CourseSection.CourseId, curriculumSemesterMap))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if two courses share the same academic year in ANY department.
    /// Returns true if there exists at least one department where both courses
    /// have defined semesters and those semesters fall in the same year pair (1-2, 3-4, etc.)
    /// </summary>
    private static bool SharesSameYearCurriculumAnyDepartment(
        int courseIdA, int courseIdB,
        Dictionary<int, List<(int DepartmentId, int SuggestedSemester)>> curriculumSemesterMap)
    {
        if (!curriculumSemesterMap.TryGetValue(courseIdA, out var semestersA)) return false;
        if (!curriculumSemesterMap.TryGetValue(courseIdB, out var semestersB)) return false;

        foreach (var (deptA, semA) in semestersA)
        {
            if (semA <= 0) continue;
            foreach (var (deptB, semB) in semestersB)
            {
                if (semB <= 0) continue;
                if (deptA != deptB) continue;
                if ((semA - 1) / 2 == (semB - 1) / 2) return true;
            }
        }
        return false;
    }

    private static decimal NormalizeAutoScheduleScore(double rawScore)
    {
        const decimal maxScore = 155m;
        return Math.Clamp((decimal)rawScore / maxScore, 0m, 1m);
    }

    private record ScheduleSlotSimple(DayOfWeek Day, TimeOnly StartTime, TimeOnly EndTime);
    private sealed record AutoScheduleCandidate(Classroom Classroom, DayOfWeek Day, TimeOnly Start, TimeOnly End, double Score);

    private sealed class AssignmentResult
    {
        public int ClassroomId { get; init; }
        public string ClassroomName { get; init; } = "";
        public List<ScheduleSlotDto> Slots { get; init; } = new();
        public double TotalScore { get; init; }
    }

    /// <summary>
    /// Finds the best conflict-free assignment for a section across ALL blocks.
    /// All blocks use the SAME classroom. For split courses (theory+lab),
    /// each block gets its own time window but same classroom.
    /// Returns null if no valid assignment exists.
    /// </summary>
    private AssignmentResult? FindBestAssignment(
        CourseSection section,
        List<Classroom> candidateClassrooms,
        Dictionary<DayOfWeek, List<TimeSlot>> timeSlotsByDay,
        int[] blockSizes,
        ClassroomType requiredType,
        Dictionary<int, List<ScheduleSlotSimple>> scheduledSlotsByClassroom,
        List<(int sectionId, int courseId, int classroomId, DayOfWeek day, TimeOnly start, TimeOnly end)> previewAssignments,
        Dictionary<int, List<ScheduleSlotSimple>> scheduledSlotsByInstructor,
        List<(int instructorId, DayOfWeek day, TimeOnly start, TimeOnly end)> previewInstructorAssignments,
        List<SectionSchedule> allScheduledRecords,
        Dictionary<int, List<(int DepartmentId, int SuggestedSemester)>> curriculumSemesterMap)
    {
        AssignmentResult? bestOverall = null;

        foreach (var classroom in candidateClassrooms)
        {
            // Build all valid time windows for this classroom
            var validWindows = new List<(DayOfWeek day, TimeOnly start, TimeOnly end, int slotOrder, double score)>();

            foreach (var (day, daySlots) in timeSlotsByDay)
            {
                // Try all possible block sizes for this day
                foreach (var blockSize in blockSizes.Distinct())
                {
                    for (var startIdx = 0; startIdx <= daySlots.Count - blockSize; startIdx++)
                    {
                        var slotGroup = daySlots.Skip(startIdx).Take(blockSize).ToList();

                        // Verify consecutive (no lunch gap)
                        var isConsecutive = true;
                        for (var i = 1; i < slotGroup.Count; i++)
                        {
                            var gap = (slotGroup[i].StartTime - slotGroup[i - 1].EndTime).TotalMinutes;
                            if (gap > 15) { isConsecutive = false; break; }
                        }
                        if (!isConsecutive) continue;

                        var start = slotGroup.First().StartTime;
                        var end = slotGroup.Last().EndTime;

                        // H1: Classroom conflict
                        if (scheduledSlotsByClassroom.TryGetValue(classroom.Id, out var cls)
                            && cls.Any(s => s.Day == day && s.StartTime < end && s.EndTime > start))
                            continue;
                        if (previewAssignments.Any(p =>
                            p.classroomId == classroom.Id && p.day == day && p.start < end && p.end > start))
                            continue;

                        // H2: Instructor conflict
                        if (section.InstructorId.HasValue)
                        {
                            var instrId = section.InstructorId.Value;
                            if (scheduledSlotsByInstructor.TryGetValue(instrId, out var isl)
                                && isl.Any(s => s.Day == day && s.StartTime < end && s.EndTime > start))
                                continue;
                            if (previewInstructorAssignments.Any(p =>
                                p.instructorId == instrId && p.day == day && p.start < end && p.end > start))
                                continue;
                        }

                        // H5: Curriculum conflict
                        if (HasCurriculumConflict(section, day, start, end,
                            previewAssignments, allScheduledRecords, curriculumSemesterMap))
                            continue;

                        // H8: Same course section conflict
                        if (previewAssignments.Any(p =>
                            p.courseId == section.CourseId && p.sectionId != section.Id
                            && p.day == day && p.start < end && p.end > start))
                            continue;

                        var score = CalculateAutoScheduleScore(
                            section, classroom, requiredType, day,
                            slotGroup.First().SlotOrder, blockSize,
                            scheduledSlotsByInstructor, previewInstructorAssignments,
                            previewAssignments);

                        validWindows.Add((day, start, end, slotGroup.First().SlotOrder, score));
                    }
                }
            }

            // Now find the best combination of non-overlapping windows for all blocks
            var bestForClassroom = FindBestBlockCombination(validWindows, blockSizes, section.InstructorId,
                scheduledSlotsByInstructor, previewInstructorAssignments);

            if (bestForClassroom != null)
            {
                var totalScore = bestForClassroom.Sum(w => w.score);
                if (bestOverall == null || totalScore > bestOverall.TotalScore)
                {
                    bestOverall = new AssignmentResult
                    {
                        ClassroomId = classroom.Id,
                        ClassroomName = classroom.Name,
                        Slots = bestForClassroom.Select(w => new ScheduleSlotDto
                        {
                            Day = w.day, StartTime = w.start, EndTime = w.end
                        }).ToList(),
                        TotalScore = totalScore
                    };
                }
            }
        }

        return bestOverall;
    }

    /// <summary>
    /// Finds the best non-overlapping combination of time windows for the given block sizes.
    /// For single-block courses, just picks the highest-scoring window.
    /// For split courses, finds two non-overlapping windows (one per block size).
    /// Also checks instructor daily load across selected blocks.
    /// </summary>
    private static List<(DayOfWeek day, TimeOnly start, TimeOnly end, double score)>? FindBestBlockCombination(
        List<(DayOfWeek day, TimeOnly start, TimeOnly end, int slotOrder, double score)> validWindows,
        int[] blockSizes,
        int? instructorId,
        Dictionary<int, List<ScheduleSlotSimple>> scheduledSlotsByInstructor,
        List<(int instructorId, DayOfWeek day, TimeOnly start, TimeOnly end)> previewInstructorAssignments)
    {
        if (blockSizes.Length == 1)
        {
            // Single block: find windows matching the block size and pick best
            var slotCount = blockSizes[0];
            var matching = validWindows
                .Where(w => CountSlots(w.start, w.end) == slotCount)
                .OrderByDescending(w => w.score)
                .ThenBy(w => w.day)
                .ThenBy(w => w.start)
                .FirstOrDefault();

            if (matching == default) return null;
            return new List<(DayOfWeek, TimeOnly, TimeOnly, double)>
            {
                (matching.day, matching.start, matching.end, matching.score)
            };
        }

        // Multi-block: find best pair of non-overlapping windows
        var block0Size = blockSizes[0];
        var block1Size = blockSizes[1];

        var block0Windows = validWindows
            .Where(w => CountSlots(w.start, w.end) == block0Size)
            .OrderByDescending(w => w.score)
            .Take(20) // limit search space
            .ToList();

        var block1Windows = validWindows
            .Where(w => CountSlots(w.start, w.end) == block1Size)
            .OrderByDescending(w => w.score)
            .Take(20)
            .ToList();

        (DayOfWeek day, TimeOnly start, TimeOnly end, double score)? bestW0 = null;
        (DayOfWeek day, TimeOnly start, TimeOnly end, double score)? bestW1 = null;
        var bestPairScore = double.MinValue;

        foreach (var w0 in block0Windows)
        {
            foreach (var w1 in block1Windows)
            {
                // Must not overlap in time (same or different days)
                if (w0.day == w1.day && w0.start < w1.end && w1.start < w0.end)
                    continue;

                // Instructor daily load check across both blocks
                if (instructorId.HasValue)
                {
                    var instrId = instructorId.Value;
                    // Check instructor overlap between the two blocks
                    if (w0.day == w1.day)
                    {
                        var existingHours = (scheduledSlotsByInstructor.TryGetValue(instrId, out var isl)
                            ? isl.Where(s => s.Day == w0.day).Sum(s => (s.EndTime - s.StartTime).TotalHours) : 0)
                            + previewInstructorAssignments.Where(p => p.instructorId == instrId && p.day == w0.day)
                                .Sum(p => (p.end - p.start).TotalHours);
                        if (existingHours + block0Size + block1Size > 9) continue;
                    }
                }

                var pairScore = w0.score + w1.score;
                if (pairScore > bestPairScore)
                {
                    bestPairScore = pairScore;
                    bestW0 = (w0.day, w0.start, w0.end, w0.score);
                    bestW1 = (w1.day, w1.start, w1.end, w1.score);
                }
            }
        }

        if (bestW0 == null || bestW1 == null) return null;
        return new List<(DayOfWeek, TimeOnly, TimeOnly, double)> { bestW0.Value, bestW1.Value };
    }

    private static int CountSlots(TimeOnly start, TimeOnly end)
    {
        // Each slot is ~55 minutes (50 min + 5 min break)
        var totalMinutes = (end - start).TotalMinutes;
        return (int)Math.Round(totalMinutes / 55.0);
    }

    /// <summary>
    /// Internships (490), graduation projects (491, 492) are not held in classrooms.
    /// </summary>
    private static bool IsNonSchedulableCourse(string courseCode)
    {
        if (courseCode.Length < 4) return false;
        var suffix = courseCode[^3..]; // last 3 characters
        return suffix is "490" or "491" or "492";
    }

    /// <summary>
    /// Lab and Computer classrooms are interchangeable for lab courses.
    /// </summary>
    private static bool IsCompatibleClassroomType(ClassroomType actual, ClassroomType required) => required switch
    {
        ClassroomType.Lab => actual is ClassroomType.Lab or ClassroomType.Computer,
        ClassroomType.Lecture => actual is ClassroomType.Lecture or ClassroomType.Amphitheater,
        _ => actual == required
    };

    private static string GetTermStatusLabel(TermStatus s) => s switch
    {
        TermStatus.Draft            => "Taslak",
        TermStatus.Planning         => "Planlama",
        TermStatus.Scheduling       => "Programlama",
        TermStatus.Published        => "Yayınlandı",
        TermStatus.RegistrationOpen => "Kayıt Açık",
        TermStatus.AddDrop          => "Ders Ekle/Bırak",
        TermStatus.WithdrawalPeriod => "Çekilme Dönemi",
        TermStatus.GradeEntry       => "Not Girişi",
        TermStatus.Closed           => "Kapalı",
        TermStatus.Archived         => "Arşivlendi",
        _                           => s.ToString()
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
                DisplayName = ts.DisplayName
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
