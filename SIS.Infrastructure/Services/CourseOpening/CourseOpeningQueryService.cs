using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseOpening.DTOs;
using SIS.Application.CourseOpening.Services;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.CourseOpening;

/// <summary>
/// Ders açma akışı için liste/lookup sorguları (read-only).
/// Faz 3.C: Eski CourseOpeningService'ten ayrıldı.
/// </summary>
public class CourseOpeningQueryService(
    AppDbContext context,
    IEnrollmentForecastService forecastService,
    CourseOpeningHelpers helpers) : ICourseOpeningQueryService
{
    private readonly AppDbContext _context = context;
    private readonly IEnrollmentForecastService _forecastService = forecastService;
    private readonly CourseOpeningHelpers _helpers = helpers;

    public async Task<OpenedCourseListDto> GetOpenedCoursesAsync(int departmentId, int termId, CancellationToken ct = default)
    {
        var department = await _context.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == departmentId, ct)
            ?? throw new InvalidOperationException("Bölüm bulunamadı.");
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");

        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course).ThenInclude(c => c.Department)
            .Include(cs => cs.Instructor)
            .Where(cs => cs.TermId == termId
                && (cs.Course.DepartmentId == departmentId
                    || _context.CourseAccessPolicies.Any(cap =>
                        cap.CourseId == cs.CourseId
                        && cap.TermId == termId
                        && cap.DepartmentId == departmentId)))
            .OrderBy(cs => cs.Course.Code).ThenBy(cs => cs.Id)
            .ToListAsync(ct);

        var instructorIds = sections.Where(s => s.InstructorId.HasValue).Select(s => s.InstructorId!.Value).Distinct().ToList();
        var instructorNameMap = await _helpers.LoadInstructorNameMapAsync(instructorIds, ct);

        var sectionIds = sections.Select(s => s.Id).ToList();
        var registeredCounts = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(r => sectionIds.Contains(r.CourseSectionId) && r.Status != RegistrationStatus.Dropped && r.Status != RegistrationStatus.Draft)
            .GroupBy(r => r.CourseSectionId)
            .Select(g => new { SectionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SectionId, x => x.Count, ct);

        var draftOrPendingCounts = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(r =>
                sectionIds.Contains(r.CourseSectionId)
                && (r.Status == RegistrationStatus.Draft || r.Status == RegistrationStatus.PendingAdvisorApproval))
            .GroupBy(r => r.CourseSectionId)
            .Select(g => new { SectionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SectionId, x => x.Count, ct);

        var courseIds = sections.Select(s => s.CourseId).Distinct().ToList();
        var suggestedSemesters = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => courseIds.Contains(cc.CourseId) && cc.Curriculum.Program.DepartmentId == departmentId && cc.Curriculum.IsActive)
            .GroupBy(cc => cc.CourseId)
            .Select(g => new { CourseId = g.Key, Semester = g.Min(cc => cc.SuggestedSemester) })
            .ToDictionaryAsync(x => x.CourseId, x => x.Semester, ct);

        var sectionNumberMap = new Dictionary<int, int>();
        var courseGroupCounters = new Dictionary<int, int>();
        foreach (var s in sections)
        {
            if (!courseGroupCounters.TryGetValue(s.CourseId, out var counter))
                counter = 0;
            counter++;
            courseGroupCounters[s.CourseId] = counter;
            sectionNumberMap[s.Id] = counter;
        }

        var dtos = sections.Select(s =>
        {
            instructorNameMap.TryGetValue(s.InstructorId ?? 0, out var instrName);
            registeredCounts.TryGetValue(s.Id, out var regCount);
            draftOrPendingCounts.TryGetValue(s.Id, out var draftOrPendingCount);
            suggestedSemesters.TryGetValue(s.CourseId, out var sugSem);

            return new OpenedCourseSectionDto
            {
                SectionId = s.Id,
                SectionNumber = sectionNumberMap[s.Id],
                CourseId = s.CourseId,
                CourseCode = s.Course.Code,
                CourseName = s.Course.Name,
                TheoryHours = s.Course.TheoryHours,
                LabHours = s.Course.LabHours,
                Credits = s.Course.TheoryHours + s.Course.LabHours,
                Ects = s.Course.Ects,
                SuggestedSemester = sugSem,
                Capacity = s.Capacity,
                InternalQuota = s.InternalQuota,
                ExternalQuota = s.ExternalQuota,
                EnrolledCount = s.EnrolledCount,
                RegisteredCount = regCount,
                DraftOrPendingCount = draftOrPendingCount,
                InstructorId = s.InstructorId,
                InstructorName = instrName,
                IsActive = s.IsActive,
                DepartmentId = s.Course.DepartmentId,
                DepartmentName = s.Course.Department?.Name,
            };
        }).ToList();

        return new OpenedCourseListDto
        {
            DepartmentId = departmentId,
            DepartmentName = department.Name,
            TermId = termId,
            TermName = term.Name,
            TotalSections = dtos.Count,
            Sections = dtos,
        };
    }

    public async Task<List<CurriculumCourseForOpeningDto>> GetCurriculumCoursesForOpeningAsync(int departmentId, int termId, bool showAll = false, CancellationToken ct = default)
    {
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId, ct);
        var semesterParity = term?.TermType == TermType.Spring ? 0 : 1;

        var curriculumCourses = await _context.CurriculumCourses
            .AsNoTracking()
            .Include(cc => cc.Course)
            .Where(cc => cc.Curriculum.IsActive && cc.Curriculum.Program.DepartmentId == departmentId)
            .Where(cc => showAll || term == null || term.TermType == TermType.Summer || cc.SuggestedSemester % 2 == semesterParity)
            .OrderBy(cc => cc.SuggestedSemester).ThenBy(cc => cc.Course.Code)
            .ToListAsync(ct);

        var pools = await _context.CurriculumElectivePools
            .AsNoTracking()
            .Include(p => p.ElectiveGroup)
                .ThenInclude(g => g.Courses)
                    .ThenInclude(egc => egc.Course)
            .Where(p => p.Curriculum.IsActive && p.Curriculum.Program.DepartmentId == departmentId)
            .Where(p => showAll
                     || term == null
                     || term.TermType == TermType.Summer
                     || (p.SuggestedSemesterEnd - p.SuggestedSemesterStart) >= 1
                     || p.SuggestedSemesterStart % 2 == semesterParity)
            .ToListAsync(ct);

        var poolCourseEntries = pools
            .SelectMany(p => p.ElectiveGroup.Courses.Select(egc => new
            {
                PoolId = p.Id,
                PoolStart = p.SuggestedSemesterStart,
                PoolEnd = p.SuggestedSemesterEnd,
                Group = p.ElectiveGroup,
                Course = egc.Course,
            }))
            .ToList();

        var allCourseIds = curriculumCourses.Select(cc => cc.CourseId)
            .Concat(poolCourseEntries.Select(pce => pce.Course.Id))
            .Distinct()
            .ToList();

        var existingSectionCounts = await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.TermId == termId && cs.Status == CourseSectionStatus.Active && allCourseIds.Contains(cs.CourseId))
            .GroupBy(cs => cs.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Count, ct);

        var forecastSummary = await _forecastService.GetDepartmentSummaryAsync(departmentId);
        var forecastMap = (forecastSummary?.CourseSummaries ?? [])
            .Where(summary => allCourseIds.Contains(summary.CourseId))
            .ToDictionary(summary => summary.CourseId);

        var isTermInPlanning = term != null && !term.IsCancelled;

        var resultMap = new Dictionary<int, CurriculumCourseForOpeningDto>();

        foreach (var g in curriculumCourses.GroupBy(cc => cc.CourseId))
        {
            var first = g.First();
            existingSectionCounts.TryGetValue(first.CourseId, out var sectionCount);
            forecastMap.TryGetValue(first.CourseId, out var forecast);
            var hasOwner = first.Course.DepartmentId.HasValue;
            var isOwner = hasOwner && first.Course.DepartmentId == departmentId;
            var blockReason = ResolveBlockReason(isTermInPlanning, hasOwner, isOwner);

            resultMap[first.CourseId] = new CurriculumCourseForOpeningDto
            {
                CourseId = first.CourseId,
                CourseCode = first.Course.Code,
                CourseName = first.Course.Name,
                TheoryHours = first.Course.TheoryHours,
                LabHours = first.Course.LabHours,
                Ects = first.Course.Ects,
                SuggestedSemester = first.SuggestedSemester,
                IsCompulsory = first.IsCompulsory,
                AlreadyOpened = sectionCount > 0,
                ExistingSectionCount = sectionCount,
                PredictedDemand = forecast?.TotalPredictedDemand ?? 0,
                RetakeCount = forecast?.DemandBreakdown.TotalRetake ?? 0,
                RecommendedSectionCount = forecast?.RecommendedSectionCount ?? 0,
                RecommendedCapacity = forecast?.SectionDistribution.EffectiveCapacityPerSection ?? 0,
                CrossDepartmentDemand = (forecast?.CrossDepartmentDemand ?? [])
                    .Select(item => new CrossDepartmentDemandDto
                    {
                        DepartmentId = item.DepartmentId,
                        DepartmentName = item.DepartmentName,
                        ProgramName = item.ProgramName,
                        EstimatedStudentCount = item.EstimatedStudentCount,
                    })
                    .ToList(),
                PreviousTermEnrollment = forecast?.ComparableTermEnrollment,
                CanOpen = blockReason is null,
                BlockReason = blockReason,
                Source = first.IsCompulsory ? "Compulsory" : "Elective",
            };
        }

        foreach (var pce in poolCourseEntries)
        {
            if (resultMap.ContainsKey(pce.Course.Id))
                continue;

            existingSectionCounts.TryGetValue(pce.Course.Id, out var sectionCount);
            forecastMap.TryGetValue(pce.Course.Id, out var forecast);
            var hasOwner = pce.Course.DepartmentId.HasValue;
            var isOwner = hasOwner && pce.Course.DepartmentId == departmentId;
            var blockReason = ResolveBlockReason(isTermInPlanning, hasOwner, isOwner);

            resultMap[pce.Course.Id] = new CurriculumCourseForOpeningDto
            {
                CourseId = pce.Course.Id,
                CourseCode = pce.Course.Code,
                CourseName = pce.Course.Name,
                TheoryHours = pce.Course.TheoryHours,
                LabHours = pce.Course.LabHours,
                Ects = pce.Course.Ects,
                SuggestedSemester = pce.PoolStart,
                IsCompulsory = false,
                AlreadyOpened = sectionCount > 0,
                ExistingSectionCount = sectionCount,
                PredictedDemand = forecast?.TotalPredictedDemand ?? 0,
                RetakeCount = forecast?.DemandBreakdown.TotalRetake ?? 0,
                RecommendedSectionCount = forecast?.RecommendedSectionCount ?? 0,
                RecommendedCapacity = forecast?.SectionDistribution.EffectiveCapacityPerSection ?? 0,
                CrossDepartmentDemand = (forecast?.CrossDepartmentDemand ?? [])
                    .Select(item => new CrossDepartmentDemandDto
                    {
                        DepartmentId = item.DepartmentId,
                        DepartmentName = item.DepartmentName,
                        ProgramName = item.ProgramName,
                        EstimatedStudentCount = item.EstimatedStudentCount,
                    })
                    .ToList(),
                PreviousTermEnrollment = forecast?.ComparableTermEnrollment,
                CanOpen = blockReason is null,
                BlockReason = blockReason,
                ElectivePoolId = pce.PoolId,
                ElectiveGroupCode = pce.Group.Code,
                ElectiveGroupName = pce.Group.Name,
                Source = "Pool",
            };
        }

        return resultMap.Values
            .OrderBy(c => c.SuggestedSemester).ThenBy(c => c.CourseCode)
            .ToList();
    }

    private static string? ResolveBlockReason(bool isTermInPlanning, bool hasOwner, bool isOwner)
    {
        if (!isTermInPlanning) return "TermNotInPlanning";
        if (!hasOwner) return "CourseUnassigned";
        if (!isOwner) return "NotOwner";
        return null;
    }

    public async Task<List<InstructorOptionDto>> GetDepartmentInstructorsAsync(int departmentId, int? termId = null, CancellationToken ct = default)
    {
        var facultyId = await _context.Departments
            .AsNoTracking()
            .Where(d => d.Id == departmentId)
            .Select(d => d.FacultyId)
            .FirstOrDefaultAsync(ct);

        var instructors = await _context.Instructors
            .AsNoTracking()
            .Where(i => i.Department.FacultyId == facultyId)
            .Select(i => new { i.Id, i.Title, i.UserId, i.DepartmentId, i.MaxWeeklyHours })
            .ToListAsync(ct);

        var userIds = instructors.Select(i => i.UserId).Distinct().ToList();
        var userMap = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.FirstName, u.LastName }, ct);

        var deptIds = instructors.Select(i => i.DepartmentId).Distinct().ToList();
        var deptNameMap = await _context.Departments
            .AsNoTracking()
            .Where(d => deptIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Code, ct);

        var workloadMap = new Dictionary<int, int>();
        if (termId.HasValue)
        {
            var instructorIds = instructors.Select(i => i.Id).ToList();
            workloadMap = await _context.CourseSections
                .AsNoTracking()
                .Where(cs => instructorIds.Contains(cs.InstructorId!.Value) && cs.TermId == termId.Value && cs.Status == CourseSectionStatus.Active)
                .GroupBy(cs => cs.InstructorId!.Value)
                .Select(g => new { InstructorId = g.Key, TotalHours = g.Sum(cs => cs.Course.TheoryHours + cs.Course.LabHours) })
                .ToDictionaryAsync(x => x.InstructorId, x => x.TotalHours, ct);
        }

        return instructors.Select(i =>
        {
            var baseName = userMap.TryGetValue(i.UserId, out var u) ? $"{i.Title} {u.FirstName} {u.LastName}" : $"{i.Title} (#{i.Id})";
            var name = i.DepartmentId != departmentId && deptNameMap.TryGetValue(i.DepartmentId, out var deptCode)
                ? $"{baseName} ({deptCode})"
                : baseName;
            workloadMap.TryGetValue(i.Id, out var assignedHours);
            return new InstructorOptionDto
            {
                InstructorId = i.Id,
                FullName = name,
                MaxWeeklyHours = i.MaxWeeklyHours,
                AssignedWeeklyHours = assignedHours,
            };
        })
        .OrderByDescending(i => instructors.First(x => x.Id == i.InstructorId).DepartmentId == departmentId ? 1 : 0)
        .ThenBy(i => i.FullName)
        .ToList();
    }

    public async Task<List<ExternalCourseDto>> GetExternalCoursesAsync(int departmentId, int termId, CancellationToken ct = default)
    {
        var externalCourseIds = await _context.CurriculumCourses
            .AsNoTracking()
            .Include(cc => cc.Course)
            .Where(cc => cc.Curriculum.IsActive
                && cc.Curriculum.Program.DepartmentId == departmentId
                && cc.Course.DepartmentId.HasValue
                && cc.Course.DepartmentId != departmentId)
            .Select(cc => cc.CourseId)
            .Distinct()
            .ToListAsync(ct);

        if (externalCourseIds.Count == 0)
            return [];

        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course).ThenInclude(c => c.Department)
            .Where(cs => cs.TermId == termId && cs.Status == CourseSectionStatus.Active && externalCourseIds.Contains(cs.CourseId))
            .ToListAsync(ct);

        var forecastSummary = await _forecastService.GetDepartmentSummaryAsync(departmentId);
        var forecastMap = (forecastSummary?.CourseSummaries ?? [])
            .Where(s => externalCourseIds.Contains(s.CourseId))
            .ToDictionary(s => s.CourseId);

        return sections
            .GroupBy(cs => cs.CourseId)
            .Select(g =>
            {
                var first = g.First();
                forecastMap.TryGetValue(first.CourseId, out var forecast);
                return new ExternalCourseDto
                {
                    CourseId = first.CourseId,
                    CourseCode = first.Course.Code,
                    CourseName = first.Course.Name,
                    Ects = first.Course.Ects,
                    OwnerDepartmentName = first.Course.Department?.Name ?? "",
                    SectionCount = g.Count(),
                    TotalCapacity = g.Sum(s => s.Capacity),
                    TotalEnrolled = g.Sum(s => s.EnrolledCount),
                    PredictedDemand = forecast?.TotalPredictedDemand ?? 0,
                };
            })
            .OrderBy(d => d.CourseCode)
            .ToList();
    }
}
