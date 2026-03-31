using System.Text.Json;
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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppDbContext _context;
    private readonly ISchedulingService _schedulingService;

    public FacultyPlanningService(AppDbContext context, ISchedulingService schedulingService)
    {
        _context = context;
        _schedulingService = schedulingService;
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

    public async Task<FacultyPlanningOverviewDto> GetOverviewAsync(int facultyId, int termId, CancellationToken ct = default)
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

        var departmentIds = faculty.Departments.Select(d => d.Id).ToList();

        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Classroom)
            .Where(cs => cs.TermId == termId && cs.IsActive && departmentIds.Contains(cs.Course.DepartmentId ?? 0))
            .ToListAsync(ct);

        var scheduled = sections.Count(s => s.ClassroomId.HasValue && s.ScheduleJson != "[]");
        var unscheduled = sections.Count - scheduled;

        var conflicts = await _schedulingService.GetConflictsAsync(termId, ct);
        var facultyConflicts = conflicts.Where(c =>
            sections.Any(s => s.Id == c.SectionId || s.Id == c.RelatedSectionId)).ToList();

        var conflictByType = facultyConflicts
            .GroupBy(c => c.ConflictType)
            .ToDictionary(g => g.Key, g => g.Count());

        var departmentProgress = new List<DepartmentProgressDto>();
        foreach (var dept in faculty.Departments)
        {
            var deptSections = sections.Where(s => s.Course.DepartmentId == dept.Id).ToList();
            var pendingApprovals = await _context.CourseSectionApprovals
                .CountAsync(a => a.DepartmentId == dept.Id && a.TermId == termId && a.Status == SectionApprovalStatus.Pending, ct);

            departmentProgress.Add(new DepartmentProgressDto
            {
                DepartmentId = dept.Id,
                DepartmentName = dept.Name,
                DepartmentCode = dept.Code,
                TotalSections = deptSections.Count,
                ScheduledSections = deptSections.Count(s => s.ClassroomId.HasValue && s.ScheduleJson != "[]"),
                PendingApprovals = pendingApprovals
            });
        }

        var pendingWarnings = departmentProgress
            .Where(d => d.PendingApprovals > 0)
            .Select(d => new PendingApprovalWarningDto
            {
                DepartmentId = d.DepartmentId,
                DepartmentName = d.DepartmentName,
                PendingCount = d.PendingApprovals
            }).ToList();

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
                ScheduleSummary = l.ScheduleJson,
                AssignedByUserName = int.TryParse(l.UserId, out var uid) ? userNameMap.GetValueOrDefault(uid, "Bilinmeyen") : "Bilinmeyen",
                Action = l.Action,
                AssignedAt = l.CreatedAt
            }).ToList()
        };
    }

    public async Task<FacultyPlanningSectionPageDto> GetSectionsAsync(
        int facultyId, int termId, int? departmentId = null, string? status = null,
        int page = 1, int pageSize = 50, CancellationToken ct = default)
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

        var allSections = await query.OrderBy(cs => cs.Course.Code).ThenBy(cs => cs.Id).ToListAsync(ct);

        var grouped = allSections.GroupBy(s => new { s.CourseId, s.TermId });
        var items = new List<FacultyPlanningSectionDto>();

        var conflicts = await _schedulingService.GetConflictsAsync(termId, ct);
        var conflictSectionIds = conflicts.SelectMany(c => new[] { c.SectionId, c.RelatedSectionId ?? 0 }).ToHashSet();

        var instructorIds = allSections.Where(s => s.InstructorId.HasValue).Select(s => s.InstructorId!.Value).Distinct();
        var instructorNameMap = await LoadInstructorNameMapAsync(instructorIds, ct);

        foreach (var group in grouped)
        {
            var index = 0;
            foreach (var s in group.OrderBy(x => x.Id))
            {
                index++;
                var hasClassroom = s.ClassroomId.HasValue;
                var hasSchedule = s.ScheduleJson != "[]";
                var hasInstructor = s.InstructorId.HasValue;
                var hasConflict = conflictSectionIds.Contains(s.Id);

                string sectionStatus;
                if (hasConflict) sectionStatus = "Conflicted";
                else if (hasClassroom && hasSchedule) sectionStatus = "Scheduled";
                else if (hasClassroom || hasInstructor) sectionStatus = "Partial";
                else sectionStatus = "Unscheduled";

                if (status != null && !string.Equals(sectionStatus, status, StringComparison.OrdinalIgnoreCase))
                    continue;

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
                    ScheduleJson = s.ScheduleJson,
                    ScheduleSummary = BuildScheduleSummary(s.ScheduleJson),
                    Status = sectionStatus,
                    TheoryHours = s.Course.TheoryHours,
                    LabHours = s.Course.LabHours,
                    SectionIndex = index
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
        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, ct)
            ?? throw new KeyNotFoundException("Sube bulunamadi.");

        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == section.TermId, ct);
        if (term != null && term.Status != TermStatus.Scheduling)
            throw new InvalidOperationException(
                $"Hoca/derslik/zaman ataması için dönem 'Programlama' aşamasında olmalıdır. Mevcut durum: {GetTermStatusLabel(term.Status)}");

        if (section.IsFinalized)
            throw new InvalidOperationException("Bu sube kesinlesmis, degisiklik yapilamaz.");

        var userDeptId = await ResolveDepartmentForUserAsync(userId, ct);
        if (userDeptId.HasValue && section.Course.DepartmentId != userDeptId.Value)
            throw new UnauthorizedAccessException("Sadece kendi bolumunuzun subelerine atama yapabilirsiniz.");

        var result = new AssignmentResultDto { Success = true };

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
            section.ScheduleJson = JsonSerializer.Serialize(
                request.ScheduleSlots.Select(s => new { s.Day, s.StartTime, s.EndTime, s.ClassroomId }),
                JsonOptions);
        }

        if (request.InstructorId.HasValue)
        {
            var instructorExists = await _context.Instructors.AnyAsync(i => i.Id == request.InstructorId.Value, ct);
            if (!instructorExists)
                throw new KeyNotFoundException("Ogretim uyesi bulunamadi.");
            section.InstructorId = request.InstructorId.Value;
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("Bu sube baska bir kullanici tarafindan guncellendi. Lutfen yenileyip tekrar deneyin.");
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
            ScheduleJson = section.ScheduleJson,
            InstructorId = request.InstructorId
        });
        await _context.SaveChangesAsync(ct);

        return result;
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

        section.ClassroomId = null;
        section.ScheduleJson = "[]";
        section.InstructorId = null;

        _context.SchedulingAuditLogs.Add(new SchedulingAuditLog
        {
            SectionId = sectionId,
            Action = "Unassign",
            UserId = userId
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

        var query = _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course).ThenInclude(c => c.Department)
            .Include(cs => cs.Instructor)
            .Where(cs => cs.TermId == request.TermId && cs.IsActive && !cs.IsFinalized)
            .Where(cs => cs.ScheduleJson == "[]" || cs.ClassroomId == null)
            .Where(cs => departmentIds.Contains(cs.Course.DepartmentId ?? 0));

        if (request.DepartmentId.HasValue)
            query = query.Where(cs => cs.Course.DepartmentId == request.DepartmentId.Value);

        if (request.SectionIds is { Count: > 0 })
            query = query.Where(cs => request.SectionIds.Contains(cs.Id));

        var sections = await query.ToListAsync(ct);

        var courseIds = sections.Select(s => s.CourseId).Distinct().ToList();
        var compulsoryCourseIds = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => courseIds.Contains(cc.CourseId) && cc.IsCompulsory)
            .Where(cc => cc.Curriculum.IsActive)
            .Select(cc => cc.CourseId)
            .Distinct()
            .ToListAsync(ct);

        sections = sections
            .OrderByDescending(s => s.Course.LabHours > 0 ? 1 : 0)
            .ThenByDescending(s => compulsoryCourseIds.Contains(s.CourseId) ? 1 : 0)
            .ThenByDescending(s => s.Capacity)
            .ToList();

        var classrooms = await _context.Classrooms.AsNoTracking().ToListAsync(ct);
        var allScheduled = await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.TermId == request.TermId && cs.IsActive && cs.ClassroomId.HasValue && cs.ScheduleJson != "[]")
            .Select(cs => new { cs.Id, cs.ClassroomId, cs.InstructorId, cs.ScheduleJson })
            .ToListAsync(ct);

        var sectionInstructorIds = sections.Where(s => s.InstructorId.HasValue).Select(s => s.InstructorId!.Value).Distinct();
        var instructorNameMap = await LoadInstructorNameMapAsync(sectionInstructorIds, ct);

        var previewAssignments = new List<(int classroomId, DayOfWeek day, TimeOnly start, TimeOnly end)>();

        foreach (var section in sections)
        {
            result.TotalProcessed++;

            var requiredType = section.Course.LabHours > 0 && section.Course.TheoryHours == 0
                ? ClassroomType.Lab : ClassroomType.Lecture;

            var candidateClassrooms = classrooms
                .Where(c => c.Capacity >= section.Capacity)
                .OrderByDescending(c => c.Type == requiredType ? 1 : 0)
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

            var found = false;

            foreach (var classroom in candidateClassrooms)
            {
                for (var dayInt = 1; dayInt <= 5; dayInt++)
                {
                    var day = (DayOfWeek)dayInt;
                    for (var hour = 8; hour <= 17 - totalHours; hour++)
                    {
                        var start = new TimeOnly(hour, 0);
                        var end = new TimeOnly(hour + totalHours, 0);

                        var conflict = allScheduled.Any(s =>
                        {
                            if (s.ClassroomId != classroom.Id) return false;
                            try
                            {
                                var slots = JsonSerializer.Deserialize<List<ScheduleSlotSimple>>(s.ScheduleJson!, JsonOptions);
                                return slots?.Any(slot => slot.Day == day && slot.StartTime < end && slot.EndTime > start) == true;
                            }
                            catch { return false; }
                        });

                        var previewConflict = previewAssignments.Any(p =>
                            p.classroomId == classroom.Id && p.day == day && p.start < end && p.end > start);

                        if (!conflict && !previewConflict)
                        {
                            var fitScore = (classroom.Type == requiredType ? 0.5m : 0m)
                                + Math.Max(0, 0.5m - Math.Abs(classroom.Capacity - section.Capacity) / (decimal)Math.Max(classroom.Capacity, 1));

                            result.Assignments.Add(new AutoSchedulePreviewItemDto
                            {
                                SectionId = section.Id,
                                CourseCode = section.Course.Code,
                                CourseName = section.Course.Name,
                                DepartmentCode = section.Course.Department?.Code ?? "",
                                ClassroomId = classroom.Id,
                                ClassroomName = classroom.Name,
                                ScheduleSlots = new List<ScheduleSlotDto>
                                {
                                    new() { Day = day, StartTime = start, EndTime = end }
                                },
                                ScheduleSummary = $"{DayLabel(day)} {start:HH:mm}-{end:HH:mm}",
                                InstructorId = section.InstructorId,
                                InstructorName = section.InstructorId.HasValue
                                    ? instructorNameMap.GetValueOrDefault(section.InstructorId.Value) : null,
                                FitScore = fitScore
                            });

                            previewAssignments.Add((classroom.Id, day, start, end));
                            result.SuccessCount++;
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }
                if (found) break;
            }

            if (!found)
            {
                result.Failures.Add(new AutoScheduleFailureDto
                {
                    SectionId = section.Id,
                    CourseCode = section.Course.Code,
                    CourseName = section.Course.Name,
                    Reason = "Uygun zaman slotu bulunamadi (tum derslikler dolu)."
                });
                result.FailedCount++;
            }
        }

        return result;
    }

    public async Task<ApplyScheduleResultDto> ApplyScheduleAsync(ApplyScheduleRequest request, string userId, CancellationToken ct = default)
    {
        var result = new ApplyScheduleResultDto();

        foreach (var item in request.Assignments)
        {
            try
            {
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
            catch (UnauthorizedAccessException)
            {
                result.FailedCount++;
                result.Errors.Add($"Sube {item.SectionId}: Yetkiniz bulunmuyor.");
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                result.Errors.Add($"Sube {item.SectionId}: {ex.Message}");
            }
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
        var sections = await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.TermId == termId && cs.IsActive && cs.ClassroomId.HasValue && cs.ScheduleJson != "[]")
            .Select(cs => new { cs.ClassroomId, cs.ScheduleJson })
            .ToListAsync(ct);

        var occupied = new HashSet<int>();
        foreach (var s in sections)
        {
            try
            {
                var slots = JsonSerializer.Deserialize<List<ScheduleSlotSimple>>(s.ScheduleJson!, JsonOptions);
                if (slots?.Any(slot => slot.Day == day && slot.StartTime < endTime && slot.EndTime > startTime) == true)
                    occupied.Add(s.ClassroomId!.Value);
            }
            catch { /* skip invalid JSON */ }
        }
        return occupied;
    }

    private static string BuildScheduleSummary(string scheduleJson)
    {
        if (string.IsNullOrEmpty(scheduleJson) || scheduleJson == "[]") return "Zamanlanmamis";
        try
        {
            var slots = JsonSerializer.Deserialize<List<ScheduleSlotSimple>>(scheduleJson, JsonOptions);
            if (slots == null || slots.Count == 0) return "Zamanlanmamis";

            var dayLabels = new Dictionary<int, string>
            {
                [0] = "Paz", [1] = "Pzt", [2] = "Sal", [3] = "Car", [4] = "Per", [5] = "Cum", [6] = "Cmt"
            };

            return string.Join(", ", slots.Select(s =>
                $"{dayLabels.GetValueOrDefault((int)s.Day, "?")} {s.StartTime:HH:mm}-{s.EndTime:HH:mm}"));
        }
        catch { return "Gecersiz"; }
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

    private record ScheduleSlotSimple(DayOfWeek Day, TimeOnly StartTime, TimeOnly EndTime);

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
}
