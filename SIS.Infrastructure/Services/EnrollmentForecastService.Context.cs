using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SIS.Application.EnrollmentForecast.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Application.Common.Services;

namespace SIS.Infrastructure.Services;

/// <summary>
/// EnrollmentForecastService — ForecastContextData inşası.
/// Cache'leme + tüm büyük veri yükü (öğrenciler, dersler, müfredat, kayıtlar) burada.
/// Forecast/simülasyon/kalibrasyon hepsi bu context üzerinden çalışır.
/// </summary>
public partial class EnrollmentForecastService
{
    // ═══════════════════════════════════════════════════════════════
    //  FORECAST CONTEXT BUILDER
    // ═══════════════════════════════════════════════════════════════

    private async Task<ForecastContextData> GetOrBuildForecastContextAsync(int? departmentId = null)
    {
        var cacheKey = departmentId.HasValue
            ? $"{ForecastContextCacheKey}_Dept_{departmentId.Value}"
            : ForecastContextCacheKey;

        if (_cache.TryGetValue(cacheKey, out ForecastContextData? cached) && cached != null)
            return cached;

        var data = await BuildForecastContextCoreAsync(departmentId);
        _cache.Set(cacheKey, data, CacheDuration);
        return data;
    }

    private async Task<ForecastContextData> BuildForecastContextCoreAsync(int? departmentId = null)
    {
        var now = _clock.UtcNow;
        var activeTerm = await _context.AcademicTerms
            .AsNoTracking()
            .Include(t => t.AcademicCalendar)
            .FirstOrDefaultAsync(term => !term.IsCancelled && term.IsActivated && term.TermStart <= now && term.TermEnd >= now);

        var allTerms = await _context.AcademicTerms
            .AsNoTracking()
            .OrderBy(t => t.Year).ThenBy(t => t.TermType)
            .ToListAsync();

        var studentQuery = _context.Students
            .AsNoTracking()
            .Include(student => student.StudentPrograms)
                .ThenInclude(program => program.Program)
                    .ThenInclude(program => program.Department)
            .Where(student => student.Status == StudentStatus.Active && !student.HasFinancialBlock);

        // Bölüm kapsamlı: sadece ilgili bölümün öğrencilerini yükle
        if (departmentId.HasValue)
        {
            // Bölümün derslerini alan tüm öğrenciler (kendi bölümü + çapraz bölüm)
            var deptCourseIds = await _context.CurriculumCourses
                .AsNoTracking()
                .Include(cc => cc.Curriculum).ThenInclude(c => c.Program)
                .Where(cc => cc.Curriculum.Program.DepartmentId == departmentId.Value)
                .Select(cc => cc.CourseId)
                .Distinct()
                .ToListAsync();

            var deptCourseIdSet = deptCourseIds.ToHashSet();

            // Bu bölümün müfredatındaki derslerden herhangi birini alan öğrenciler
            studentQuery = studentQuery
                .Where(s => s.StudentPrograms.Any(sp =>
                    sp.Type == StudentProgramType.Main && sp.IsActive
                    && sp.Program.DepartmentId == departmentId.Value));
        }

        var activeStudents = await studentQuery.ToListAsync();

        var activeMainPrograms = activeStudents
            .Select(student => new
            {
                Student = student,
                Program = student.StudentPrograms
                    .FirstOrDefault(program => program.Type == StudentProgramType.Main && program.IsActive)
            })
            .Where(item => item.Program != null)
            .ToDictionary(item => item.Student.Id, item => item.Program!);

        var activeStudentIds = activeMainPrograms.Keys.ToList();
        var activeCurriculumIds = activeMainPrograms.Values
            .Select(program => program.CurriculumId)
            .Distinct()
            .ToList();

        var courses = await _context.Courses
            .AsNoTracking()
            .ToDictionaryAsync(course => course.Id);

        var curriculumCourses = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(curriculumCourse => activeCurriculumIds.Contains(curriculumCourse.CurriculumId))
            .ToListAsync();

        var curriculumCoursesByCurriculumId = curriculumCourses
            .GroupBy(curriculumCourse => curriculumCourse.CurriculumId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(curriculumCourse => curriculumCourse.SuggestedSemester)
                    .ThenBy(curriculumCourse => courses.TryGetValue(curriculumCourse.CourseId, out var course)
                        ? course.Code
                        : curriculumCourse.CourseId.ToString())
                    .ToList());

        var prerequisites = await _context.CoursePrerequisites
            .AsNoTracking()
            .ToListAsync();

        var prerequisitesByCourseId = prerequisites
            .GroupBy(prerequisite => prerequisite.CourseId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(prerequisite => prerequisite.RequiredCourseId)
                    .ToHashSet());

        var equivalencyPairs = await _context.CourseEquivalencies
            .AsNoTracking()
            .ToListAsync();

        var equivalentCourseIdsByCourseId = BuildEquivalentCourseMap(equivalencyPairs);

        var allSections = await _context.CourseSections
            .AsNoTracking()
            .Include(section => section.Course)
            .Include(section => section.Classroom)
            .ToListAsync();

        var registrations = activeStudentIds.Count == 0
            ? new List<CourseRegistration>()
            : await _context.CourseRegistrations
                .AsNoTracking()
                .Include(registration => registration.CourseSection)
                    .ThenInclude(section => section.Course)
                .Where(registration => activeStudentIds.Contains(registration.StudentId))
                .ToListAsync();

        var registrationsByStudentId = registrations
            .GroupBy(registration => registration.StudentId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var activeTermRegistrationsByStudentId = activeTerm == null
            ? new Dictionary<int, List<CourseRegistration>>()
            : registrations
                .Where(registration =>
                    registration.CourseSection.TermId == activeTerm.Id
                    && RegistrationRulePolicy.IsDemandStatus(registration.Status))
                .GroupBy(registration => registration.StudentId)
                .ToDictionary(group => group.Key, group => group.ToList());

        // Historical enrollment counts per course per term (from ALL registrations, not just active students)
        var historicalEnrollments = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(r => r.CourseSection)
            .Where(r => r.Status == RegistrationStatus.Active || r.Status == RegistrationStatus.Completed)
            .GroupBy(r => new { r.CourseSection.CourseId, r.CourseSection.TermId })
            .Select(g => new HistoricalEnrollmentRecord
            {
                CourseId = g.Key.CourseId,
                TermId = g.Key.TermId,
                EnrollmentCount = g.Select(r => r.StudentId).Distinct().Count()
            })
            .ToListAsync();

        var historicalByCourseThenTerm = historicalEnrollments
            .GroupBy(h => h.CourseId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(h => h.TermId, h => h.EnrollmentCount));

        var sectionCountByCourseTerm = allSections
            .Where(s => s.IsActive)
            .GroupBy(s => new { s.CourseId, s.TermId })
            .ToDictionary(g => (g.Key.CourseId, g.Key.TermId), g => g.Count());

        var termsById = allTerms.ToDictionary(t => t.Id);

        var approvalRecords = await _context.CourseSectionApprovals
            .AsNoTracking()
            .Where(a => a.ApprovedSectionCount.HasValue)
            .Select(a => new ApprovalFeedbackRecord
            {
                CourseId = a.CourseId,
                TermId = a.TermId,
                RecommendedSectionCount = a.RecommendedSectionCount,
                ApprovedSectionCount = a.ApprovedSectionCount!.Value
            })
            .ToListAsync();

        var approvalFeedbackByCourse = approvalRecords
            .GroupBy(a => a.CourseId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Show rate from calibration data
        var calibrations = await _context.ForecastCalibrations
            .AsNoTracking()
            .Where(fc => fc.ActualEnrollment > 0 && fc.EligibleStudentCount > 0)
            .ToListAsync();

        var showRateByCourse = calibrations
            .GroupBy(fc => fc.CourseId)
            .ToDictionary(
                g => g.Key,
                g => Math.Min(1.0m, g.Average(fc => fc.ShowRate)));

        // Adaptive weights from calibration data
        var adaptiveWeights = new Dictionary<int, (decimal RuleWeight, decimal HistoricalWeight)>();
        foreach (var group in calibrations.GroupBy(fc => fc.CourseId))
        {
            var records = group.OrderByDescending(fc => fc.TermId).Take(4).ToList();
            if (records.Count < 2) continue;
            var avgPctError = records.Average(r => r.PercentageError);
            var ruleWeight = avgPctError switch
            {
                > 30m => 0.35m,
                > 20m => 0.45m,
                > 10m => 0.55m,
                _ => 0.65m
            };
            adaptiveWeights[group.Key] = (ruleWeight, 1.0m - ruleWeight);
        }

        // Cohort growth by department
        var cohortData = await _context.StudentPrograms
            .AsNoTracking()
            .Where(sp => sp.Type == StudentProgramType.Main && sp.IsActive)
            .Select(sp => new { DeptId = sp.Program.DepartmentId, Year = sp.EnrollmentDate.Year })
            .ToListAsync();

        var cohortGrowthByDept = new Dictionary<int, decimal>();
        foreach (var deptGroup in cohortData.GroupBy(c => c.DeptId))
        {
            var yearCounts = deptGroup.GroupBy(c => c.Year)
                .OrderBy(g => g.Key)
                .Select(g => g.Count())
                .ToList();
            if (yearCounts.Count >= 2)
            {
                var lastYear = yearCounts[^1];
                var prevAvg = yearCounts.Take(yearCounts.Count - 1).Average();
                cohortGrowthByDept[deptGroup.Key] = prevAvg > 0 ? (decimal)lastYear / (decimal)prevAvg : 1.0m;
            }
        }

        // Classroom capacities by faculty
        List<(int FacultyId, int Capacity)> classroomCapList;
        if (activeTerm != null)
        {
            var rawClassrooms = await _context.ClassroomTermAssignments
                .AsNoTracking()
                .Where(cta => cta.TermId == activeTerm.Id)
                .Include(cta => cta.Classroom)
                .Where(cta => cta.Classroom != null)
                .Select(cta => new { cta.FacultyId, cta.Classroom!.Capacity })
                .ToListAsync();
            classroomCapList = rawClassrooms.Select(x => (x.FacultyId, x.Capacity)).ToList();
        }
        else
        {
            classroomCapList = new List<(int, int)>();
        }

        var classroomCapsByFaculty = classroomCapList
            .GroupBy(c => c.FacultyId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Capacity).OrderByDescending(c => c).ToList());

        // Department-Faculty map
        var departmentFacultyMap = await _context.Departments
            .AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.FacultyId);

        // ── Seçmeli havuz verileri ──
        var electivePoolsByCurriculum = new Dictionary<int, List<ElectivePoolContext>>();
        var electivePopularityScores = new Dictionary<int, decimal>();

        var allPools = await _context.CurriculumElectivePools
            .AsNoTracking()
            .Include(p => p.ElectiveGroup)
                .ThenInclude(g => g.Courses)
            .Where(p => activeCurriculumIds.Contains(p.CurriculumId))
            .ToListAsync();

        foreach (var pool in allPools)
        {
            var groupCourseIds = pool.ElectiveGroup.Courses.Select(c => c.CourseId).ToHashSet();
            if (groupCourseIds.Count == 0) continue;

            if (!electivePoolsByCurriculum.TryGetValue(pool.CurriculumId, out var pools))
            {
                pools = new List<ElectivePoolContext>();
                electivePoolsByCurriculum[pool.CurriculumId] = pools;
            }

            pools.Add(new ElectivePoolContext
            {
                ElectiveGroupId = pool.ElectiveGroupId,
                ElectiveGroupName = pool.ElectiveGroup.Name,
                RequiredCourseCount = pool.RequiredCourseCount,
                RequiredEcts = pool.RequiredEcts,
                SuggestedSemesterStart = pool.SuggestedSemesterStart,
                SuggestedSemesterEnd = pool.SuggestedSemesterEnd,
                CourseIdsInGroup = groupCourseIds,
            });
        }

        // Seçmeli ders popülariteleri: geçmiş dönemlerde her seçmeli gruptaki dersin kaç kez seçildiği
        var allElectiveGroupCourseIds = allPools
            .SelectMany(p => p.ElectiveGroup.Courses.Select(c => c.CourseId))
            .Distinct()
            .ToHashSet();

        if (allElectiveGroupCourseIds.Count > 0)
        {
            var electiveRegistrationCounts = await _context.CourseRegistrations
                .AsNoTracking()
                .Where(r => allElectiveGroupCourseIds.Contains(r.CourseSection.CourseId)
                    && (r.Status == RegistrationStatus.Active || r.Status == RegistrationStatus.Completed))
                .GroupBy(r => r.CourseSection.CourseId)
                .Select(g => new { CourseId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CourseId, x => x.Count);

            // Grup bazlı normalize et
            var processedGroups = new HashSet<int>();
            foreach (var poolList in electivePoolsByCurriculum.Values)
            {
                foreach (var pool in poolList)
                {
                    if (!processedGroups.Add(pool.ElectiveGroupId)) continue;

                    var groupTotal = pool.CourseIdsInGroup
                        .Sum(cid => electiveRegistrationCounts.GetValueOrDefault(cid, 0));

                    foreach (var courseId in pool.CourseIdsInGroup)
                    {
                        if (electivePopularityScores.ContainsKey(courseId)) continue;

                        electivePopularityScores[courseId] = groupTotal > 0
                            ? (decimal)electiveRegistrationCounts.GetValueOrDefault(courseId, 0) / groupTotal
                            : 1.0m / pool.CourseIdsInGroup.Count; // uniform fallback
                    }
                }
            }
        }

        var cgpaCache = new Dictionary<int, decimal>();
        var studentSnapshots = new List<StudentForecastSnapshot>();

        foreach (var student in activeStudents)
        {
            if (!activeMainPrograms.TryGetValue(student.Id, out var activeProgram))
                continue;

            var studentRegistrations = registrationsByStudentId.GetValueOrDefault(student.Id, new List<CourseRegistration>());
            var activeTermRegistrations = activeTermRegistrationsByStudentId.GetValueOrDefault(student.Id, new List<CourseRegistration>());

            var snapshot = await BuildStudentSnapshotAsync(
                student,
                activeProgram,
                studentRegistrations,
                activeTermRegistrations,
                courses,
                curriculumCoursesByCurriculumId,
                prerequisitesByCourseId,
                equivalentCourseIdsByCourseId,
                cgpaCache,
                electivePoolsByCurriculum,
                electivePopularityScores);

            studentSnapshots.Add(snapshot);
        }

        // ── Attrition (drop/withdrawal) oranları ──
        var attritionRateByCourse = new Dictionary<int, decimal>();
        var attritionRateByDepartment = new Dictionary<int, decimal>();

        // Ders bazlı: karşılaştırılabilir dönemlerde (Dropped + Withdrawn) / totalEnrolled
        var attritionData = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(r => r.CourseSection).ThenInclude(s => s.Course)
            .Where(r => r.Status == RegistrationStatus.Active
                || r.Status == RegistrationStatus.Completed
                || r.Status == RegistrationStatus.Dropped
                || r.Status == RegistrationStatus.Withdrawn)
            .GroupBy(r => new { r.CourseSection.CourseId, DeptId = r.CourseSection.Course.DepartmentId })
            .Select(g => new
            {
                CourseId = g.Key.CourseId,
                DepartmentId = g.Key.DeptId ?? 0,
                Total = g.Count(),
                DroppedOrWithdrawn = g.Count(r => r.Status == RegistrationStatus.Dropped || r.Status == RegistrationStatus.Withdrawn)
            })
            .ToListAsync();

        foreach (var item in attritionData.Where(a => a.Total >= 10)) // minimum 10 kayıt güvenilirlik eşiği
        {
            attritionRateByCourse[item.CourseId] = Math.Min(0.50m, (decimal)item.DroppedOrWithdrawn / item.Total);
        }

        // Bölüm bazlı fallback
        foreach (var deptGroup in attritionData.GroupBy(a => a.DepartmentId).Where(g => g.Key > 0))
        {
            var deptTotal = deptGroup.Sum(a => a.Total);
            var deptDropped = deptGroup.Sum(a => a.DroppedOrWithdrawn);
            if (deptTotal >= 20)
                attritionRateByDepartment[deptGroup.Key] = Math.Min(0.50m, (decimal)deptDropped / deptTotal);
        }

        // ── Ardışık ders bağımlılığı: mevcut dönem kayıt sayıları ──
        var currentTermEnrollmentByCourse = new Dictionary<int, int>();
        if (activeTerm != null)
        {
            var currentEnrollments = await _context.CourseRegistrations
                .AsNoTracking()
                .Where(r => r.CourseSection.TermId == activeTerm.Id
                    && (r.Status == RegistrationStatus.Active || r.Status == RegistrationStatus.Draft || r.Status == RegistrationStatus.PendingAdvisorApproval))
                .GroupBy(r => r.CourseSection.CourseId)
                .Select(g => new { CourseId = g.Key, Count = g.Select(r => r.StudentId).Distinct().Count() })
                .ToListAsync();

            foreach (var item in currentEnrollments)
                currentTermEnrollmentByCourse[item.CourseId] = item.Count;
        }

        // ── Ders volatilite skoru (CV) ──
        var volatilityScoreByCourse = new Dictionary<int, decimal>();
        foreach (var (courseId, termEnrollments) in historicalByCourseThenTerm)
        {
            // Karşılaştırılabilir dönem verilerini filtrele
            var comparableCounts = activeTerm != null
                ? termEnrollments
                    .Where(kv => termsById.TryGetValue(kv.Key, out var t) && t.TermType == activeTerm.TermType)
                    .Select(kv => (double)kv.Value)
                    .ToList()
                : termEnrollments.Values.Select(v => (double)v).ToList();

            if (comparableCounts.Count >= 2)
            {
                var mean = comparableCounts.Average();
                if (mean > 0)
                {
                    var variance = comparableCounts.Sum(x => Math.Pow(x - mean, 2)) / comparableCounts.Count;
                    var cv = (decimal)(Math.Sqrt(variance) / mean);
                    volatilityScoreByCourse[courseId] = Math.Round(cv, 3);
                }
            }
        }

        return new ForecastContextData
        {
            ActiveTerm = activeTerm,
            AllTerms = allTerms,
            TermsById = termsById,
            Courses = courses,
            AllSections = allSections,
            StudentSnapshots = studentSnapshots,
            ActiveCurriculumCourseIds = curriculumCourses.Select(curriculumCourse => curriculumCourse.CourseId).ToHashSet(),
            ActiveTermSectionCourseIds = activeTerm == null
                ? new HashSet<int>()
                : allSections
                    .Where(section => section.TermId == activeTerm.Id && section.Status == CourseSectionStatus.Active)
                    .Select(section => section.CourseId)
                    .ToHashSet(),
            HistoricalEnrollmentsByCourse = historicalByCourseThenTerm,
            SectionCountByCourseTerm = sectionCountByCourseTerm,
            GlobalNotes = BuildGlobalNotes(activeTerm, activeStudents.Count, activeMainPrograms.Count),
            ApprovalFeedbackByCourse = approvalFeedbackByCourse,
            ShowRateByCourse = showRateByCourse,
            CohortGrowthByDepartment = cohortGrowthByDept,
            ClassroomCapacitiesByFaculty = classroomCapsByFaculty,
            DepartmentFacultyMap = departmentFacultyMap,
            AdaptiveWeightsByCourse = adaptiveWeights,
            ElectivePoolsByCurriculum = electivePoolsByCurriculum,
            ElectivePopularityScores = electivePopularityScores,
            AttritionRateByCourse = attritionRateByCourse,
            AttritionRateByDepartment = attritionRateByDepartment,
            CurrentTermEnrollmentByCourse = currentTermEnrollmentByCourse,
            PrerequisitesByCourseId = prerequisitesByCourseId,
            VolatilityScoreByCourse = volatilityScoreByCourse
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  STUDENT SNAPSHOT BUILDER (enhanced with grade-level breakdown)
    // ═══════════════════════════════════════════════════════════════

}
