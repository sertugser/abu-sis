using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIS.Application.Common.Services;
using SIS.Application.CourseOpening.DTOs;
using SIS.Application.CourseOpening.Services;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.CourseOpening;

/// <summary>
/// Geçmiş dönemden ders aktarımı (Ders Aç (Hareket) sayfası).
/// Section açma akışını <see cref="ICourseSectionLifecycleService"/>'e delege eder.
/// Faz 3.C: Eski CourseTransferService yeniden adlandırıldı + helper'a bağlandı.
/// </summary>
public class TermCourseTransferService(
    AppDbContext context,
    IEnrollmentForecastService forecastService,
    ISystemConfigProvider systemConfig,
    ICourseSectionLifecycleService lifecycleService,
    CourseOpeningHelpers helpers,
    ILogger<TermCourseTransferService> logger) : ITermCourseTransferService
{
    private const int DefaultCapacityFallback = 40;
    private const string DefaultCapacityKey = "CourseOpening.DefaultCapacityPerSection";

    private readonly AppDbContext _context = context;
    private readonly IEnrollmentForecastService _forecastService = forecastService;
    private readonly ISystemConfigProvider _systemConfig = systemConfig;
    private readonly ICourseSectionLifecycleService _lifecycleService = lifecycleService;
    private readonly CourseOpeningHelpers _helpers = helpers;
    private readonly ILogger<TermCourseTransferService> _logger = logger;

    public async Task<List<PreviousTermCourseDto>> GetPreviousTermCoursesAsync(
        int departmentId, int sourceTermId, int targetTermId,
        int? programId = null, int? classLevel = null, CancellationToken ct = default)
    {
        var sourceSections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Where(cs => cs.TermId == sourceTermId && cs.Status == CourseSectionStatus.Active && cs.Course.DepartmentId == departmentId)
            .OrderBy(cs => cs.Course.Code).ThenBy(cs => cs.Id)
            .ToListAsync(ct);

        if (sourceSections.Count == 0)
            return [];

        var courseIds = sourceSections.Select(cs => cs.CourseId).Distinct().ToList();

        var alreadyOpenedCourseIds = await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.TermId == targetTermId && cs.Status == CourseSectionStatus.Active && courseIds.Contains(cs.CourseId))
            .Select(cs => cs.CourseId)
            .Distinct()
            .ToListAsync(ct);
        var alreadyOpenedSet = new HashSet<int>(alreadyOpenedCourseIds);

        var curriculumQuery = _context.CurriculumCourses.AsNoTracking()
            .Where(cc => courseIds.Contains(cc.CourseId)
                && cc.Curriculum.IsActive
                && cc.Curriculum.Program.DepartmentId == departmentId);

        if (programId.HasValue)
            curriculumQuery = curriculumQuery.Where(cc => cc.Curriculum.ProgramId == programId.Value);

        var suggestedSemesters = await curriculumQuery
            .GroupBy(cc => cc.CourseId)
            .Select(g => new { CourseId = g.Key, Semester = g.Min(cc => cc.SuggestedSemester), IsCompulsory = g.Any(cc => cc.IsCompulsory) })
            .ToDictionaryAsync(x => x.CourseId, ct);

        if (programId.HasValue)
        {
            var programCourseIds = suggestedSemesters.Keys.ToHashSet();
            sourceSections = sourceSections.Where(cs => programCourseIds.Contains(cs.CourseId)).ToList();
            if (sourceSections.Count == 0) return [];
            courseIds = sourceSections.Select(cs => cs.CourseId).Distinct().ToList();
        }

        var hasCrossListSet = sourceSections
            .Where(cs => cs.CrossListGroupId.HasValue)
            .Select(cs => cs.CourseId)
            .ToHashSet();

        var sourceSectionIds = sourceSections.Select(cs => cs.Id).ToList();
        var coursesWithSchedule = await _context.SectionSchedules
            .AsNoTracking()
            .Where(ss => sourceSectionIds.Contains(ss.CourseSectionId))
            .Select(ss => ss.CourseSection.CourseId)
            .Distinct()
            .ToListAsync(ct);
        var hasScheduleSet = new HashSet<int>(coursesWithSchedule);

        var accessPolicies = await _context.CourseAccessPolicies
            .AsNoTracking()
            .Where(cap => cap.TermId == sourceTermId && courseIds.Contains(cap.CourseId) && cap.DepartmentId.HasValue)
            .GroupBy(cap => cap.CourseId)
            .Select(g => new { CourseId = g.Key, DeptIds = g.Select(x => x.DepartmentId!.Value).ToList() })
            .ToDictionaryAsync(x => x.CourseId, x => x.DeptIds, ct);

        var forecastSummary = await _forecastService.GetDepartmentSummaryAsync(departmentId);
        var forecastMap = (forecastSummary?.CourseSummaries ?? [])
            .Where(s => courseIds.Contains(s.CourseId))
            .ToDictionary(s => s.CourseId, s => s.TotalPredictedDemand);

        var instructorIds = sourceSections.Where(cs => cs.InstructorId.HasValue).Select(cs => cs.InstructorId!.Value).Distinct().ToList();
        var instructorNameMap = await _helpers.LoadInstructorNameMapAsync(instructorIds, ct);

        var dtos = sourceSections
            .GroupBy(cs => cs.CourseId)
            .Select(g =>
            {
                var first = g.First();
                var sectionCount = g.Count();
                var totalCapacity = g.Sum(cs => cs.Capacity);
                var totalEnrolled = g.Sum(cs => cs.EnrolledCount);
                var instrSection = g.FirstOrDefault(cs => cs.InstructorId.HasValue);
                int? instrId = instrSection?.InstructorId;
                instructorNameMap.TryGetValue(instrId ?? 0, out var instrName);
                suggestedSemesters.TryGetValue(first.CourseId, out var currInfo);
                forecastMap.TryGetValue(first.CourseId, out var predicted);
                accessPolicies.TryGetValue(first.CourseId, out var deptIds);

                return new PreviousTermCourseDto(
                    CourseId: first.CourseId,
                    CourseCode: first.Course.Code,
                    CourseName: first.Course.Name,
                    Ects: first.Course.Ects,
                    SuggestedSemester: currInfo?.Semester ?? 0,
                    IsCompulsory: currInfo?.IsCompulsory ?? false,
                    SectionCount: sectionCount,
                    TotalCapacity: totalCapacity,
                    InstructorName: instrName,
                    InstructorId: instrId,
                    AlreadyOpened: alreadyOpenedSet.Contains(first.CourseId),
                    TotalEnrolled: totalEnrolled,
                    PredictedDemand: predicted == 0 ? null : predicted,
                    SourceInternalQuota: first.InternalQuota,
                    SourceExternalQuota: first.ExternalQuota,
                    SourceAccessibleDepartmentIds: deptIds,
                    HasCrossLists: hasCrossListSet.Contains(first.CourseId),
                    HasSchedule: hasScheduleSet.Contains(first.CourseId));
            })
            .ToList();

        if (classLevel.HasValue)
        {
            var levelMin = (classLevel.Value - 1) * 2 + 1;
            var levelMax = levelMin + 1;
            dtos = dtos.Where(d => d.SuggestedSemester >= levelMin && d.SuggestedSemester <= levelMax).ToList();
        }

        return dtos
            .OrderBy(d => d.SuggestedSemester).ThenBy(d => d.CourseCode)
            .ToList();
    }

    public async Task<TransferFromTermResultDto> TransferFromTermAsync(
        TransferFromTermRequest request, CancellationToken ct = default)
    {
        var result = new TransferFromTermResultDto();
        if (request.Courses.Count == 0)
            return result;

        var courseIds = request.Courses.Select(c => c.CourseId).ToList();

        var sourceSections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Where(cs => cs.TermId == request.SourceTermId
                && cs.Status == CourseSectionStatus.Active
                && cs.Course.DepartmentId == request.DepartmentId
                && courseIds.Contains(cs.CourseId))
            .ToListAsync(ct);

        var grouped = sourceSections.GroupBy(cs => cs.CourseId).ToDictionary(g => g.Key, g => g.ToList());

        Dictionary<int, int>? sourceToTargetSlotMap = null;
        HashSet<(int instId, int slotId)>? targetInstructorSlots = null;
        HashSet<(int classroomId, int slotId)>? targetClassroomSlots = null;
        var anyIncludeSchedule = request.Courses.Any(c => c.IncludeSchedule);
        if (anyIncludeSchedule)
        {
            (sourceToTargetSlotMap, targetInstructorSlots, targetClassroomSlots) =
                await PrepareScheduleTransferAsync(request.SourceTermId, request.TargetTermId, ct);
        }

        foreach (var item in request.Courses)
        {
            var courseId = item.CourseId;
            if (!grouped.TryGetValue(courseId, out var sections) || sections.Count == 0)
            {
                result.FailedCount++;
                result.Results.Add(new TransferCourseResultItem
                {
                    CourseId = courseId,
                    CourseCode = "",
                    CourseName = "",
                    Success = false,
                    Error = "Kaynak dönemde bu ders bulunamadı.",
                });
                continue;
            }

            var first = sections[0];
            var instrSection = sections.FirstOrDefault(s => s.InstructorId.HasValue);
            var configFallback = await _systemConfig.GetIntAsync(DefaultCapacityKey, DefaultCapacityFallback, ct);
            var defaultCapacity = sections.Count > 0 ? (int)Math.Round(sections.Average(s => s.Capacity)) : configFallback;

            var openRequest = new OpenCourseRequest
            {
                CourseId = courseId,
                SectionCount = item.SectionCount ?? sections.Count,
                CapacityPerSection = item.CapacityPerSection ?? defaultCapacity,
                InstructorId = item.InstructorId ?? instrSection?.InstructorId,
                InternalQuota = item.InternalQuota ?? first.InternalQuota,
                ExternalQuota = item.ExternalQuota ?? first.ExternalQuota,
                AccessibleDepartmentIds = item.AccessibleDepartmentIds,
                IncludeCrossLists = item.IncludeCrossLists,
            };

            try
            {
                var created = await _lifecycleService.OpenCourseAsync(request.DepartmentId, request.TargetTermId, openRequest, ct);
                result.SuccessCount++;
                result.TotalSectionsCreated += created.Count;
                var resultItem = new TransferCourseResultItem
                {
                    CourseId = courseId,
                    CourseCode = first.Course.Code,
                    CourseName = first.Course.Name,
                    Success = true,
                    SectionsCreated = created.Count,
                };

                if (item.IncludeSchedule && sourceToTargetSlotMap is not null
                    && targetInstructorSlots is not null && targetClassroomSlots is not null)
                {
                    await CopyScheduleForTransferAsync(
                        sections,
                        created,
                        openRequest.InstructorId,
                        sourceToTargetSlotMap,
                        targetInstructorSlots,
                        targetClassroomSlots,
                        resultItem.Warnings,
                        ct);
                }

                result.Results.Add(resultItem);
            }
            catch (InvalidOperationException ex)
            {
                result.FailedCount++;
                result.Results.Add(new TransferCourseResultItem
                {
                    CourseId = courseId,
                    CourseCode = first.Course.Code,
                    CourseName = first.Course.Name,
                    Success = false,
                    Error = ex.Message,
                });
            }
            catch (Exception ex)
            {
                var incidentId = Guid.NewGuid().ToString("N")[..8];
                _logger.LogError(ex,
                    "Transfer beklenmeyen hata. IncidentId={IncidentId} CourseId={CourseId} SourceTerm={SourceTermId} TargetTerm={TargetTermId}",
                    incidentId, courseId, request.SourceTermId, request.TargetTermId);
                result.FailedCount++;
                result.Results.Add(new TransferCourseResultItem
                {
                    CourseId = courseId,
                    CourseCode = first.Course.Code,
                    CourseName = first.Course.Name,
                    Success = false,
                    Error = $"Beklenmeyen hata: {ex.GetType().Name}. Sistem yöneticisine başvurun.",
                    IncidentId = incidentId,
                });
            }
        }

        return result;
    }

    private async Task<(Dictionary<int, int> SlotMap, HashSet<(int, int)> InstructorSlots, HashSet<(int, int)> ClassroomSlots)>
        PrepareScheduleTransferAsync(int sourceTermId, int targetTermId, CancellationToken ct)
    {
        var sourceTimeSlots = await _context.TimeSlots.AsNoTracking()
            .Where(ts => ts.TermId == sourceTermId)
            .ToListAsync(ct);
        var targetTimeSlots = await _context.TimeSlots.AsNoTracking()
            .Where(ts => ts.TermId == targetTermId)
            .ToListAsync(ct);

        var slotMap = new Dictionary<int, int>();
        foreach (var src in sourceTimeSlots)
        {
            var match = targetTimeSlots.FirstOrDefault(t =>
                t.DayOfWeek == src.DayOfWeek &&
                t.StartTime == src.StartTime &&
                t.EndTime == src.EndTime);
            if (match is not null)
                slotMap[src.Id] = match.Id;
        }

        var existing = await _context.SectionSchedules.AsNoTracking()
            .Where(ss => ss.CourseSection.TermId == targetTermId)
            .Select(ss => new { ss.TimeSlotId, ss.ClassroomId, ss.CourseSection.InstructorId })
            .ToListAsync(ct);

        var instructorSlots = existing
            .Where(x => x.InstructorId.HasValue)
            .Select(x => (x.InstructorId!.Value, x.TimeSlotId))
            .ToHashSet();

        var classroomSlots = existing
            .Where(x => x.ClassroomId.HasValue)
            .Select(x => (x.ClassroomId!.Value, x.TimeSlotId))
            .ToHashSet();

        return (slotMap, instructorSlots, classroomSlots);
    }

    private async Task CopyScheduleForTransferAsync(
        List<CourseSection> sourceSections,
        List<OpenedCourseSectionDto> createdSections,
        int? targetInstructorId,
        Dictionary<int, int> sourceToTargetSlotMap,
        HashSet<(int, int)> targetInstructorSlots,
        HashSet<(int, int)> targetClassroomSlots,
        List<string> warnings,
        CancellationToken ct)
    {
        var sortedSource = sourceSections.OrderBy(s => s.SectionNumber).ToList();
        var sortedCreated = createdSections.OrderBy(c => c.SectionNumber).ToList();
        var sourceSectionIds = sortedSource.Select(s => s.Id).ToList();

        var sourceSchedules = await _context.SectionSchedules.AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => sourceSectionIds.Contains(ss.CourseSectionId))
            .ToListAsync(ct);

        if (sourceSchedules.Count == 0)
            return;

        var pairCount = Math.Min(sortedSource.Count, sortedCreated.Count);
        var newSchedules = new List<SectionSchedule>();

        for (var i = 0; i < pairCount; i++)
        {
            var srcSection = sortedSource[i];
            var newSectionId = sortedCreated[i].SectionId;
            var newSectionNumber = sortedCreated[i].SectionNumber;
            var schedulesForSrc = sourceSchedules.Where(ss => ss.CourseSectionId == srcSection.Id).ToList();

            foreach (var ss in schedulesForSrc)
            {
                if (!sourceToTargetSlotMap.TryGetValue(ss.TimeSlotId, out var targetSlotId))
                {
                    warnings.Add($"Şube {newSectionNumber}: kaynak slot ({ss.TimeSlot.DisplayName}) hedef dönemde yok — schedule eklenmedi.");
                    continue;
                }

                if (targetInstructorId.HasValue && targetInstructorSlots.Contains((targetInstructorId.Value, targetSlotId)))
                {
                    warnings.Add($"Şube {newSectionNumber}: hoca aynı slotta ({ss.TimeSlot.DisplayName}) başka derste — schedule eklenmedi.");
                    continue;
                }

                if (ss.ClassroomId.HasValue && targetClassroomSlots.Contains((ss.ClassroomId.Value, targetSlotId)))
                {
                    warnings.Add($"Şube {newSectionNumber}: derslik aynı slotta ({ss.TimeSlot.DisplayName}) dolu — schedule eklenmedi.");
                    continue;
                }

                newSchedules.Add(new SectionSchedule
                {
                    CourseSectionId = newSectionId,
                    TimeSlotId = targetSlotId,
                    ClassroomId = ss.ClassroomId,
                    BlockType = ss.BlockType,
                });

                if (targetInstructorId.HasValue)
                    targetInstructorSlots.Add((targetInstructorId.Value, targetSlotId));
                if (ss.ClassroomId.HasValue)
                    targetClassroomSlots.Add((ss.ClassroomId.Value, targetSlotId));
            }
        }

        if (sortedCreated.Count > sortedSource.Count)
        {
            var unscheduled = sortedCreated.Count - sortedSource.Count;
            warnings.Add($"Hedef dönemde {unscheduled} ek şube açıldı — bunlara schedule kopyalanmadı (kaynak'ta karşılık yok).");
        }

        if (newSchedules.Count > 0)
        {
            _context.SectionSchedules.AddRange(newSchedules);
            await _context.SaveChangesAsync(ct);
        }
    }
}
