using Microsoft.EntityFrameworkCore;
using SIS.Application.EnrollmentForecast.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services;

/// <summary>
/// EnrollmentForecastService — öğrenci-bazlı snapshot inşası.
/// BuildStudentSnapshotAsync büyük (~218 satır) öğrencinin müfredat ilerlemesini,
/// alabileceği dersleri, ECTS hesabını çıkarır.
/// </summary>
public partial class EnrollmentForecastService
{
    private async Task<StudentForecastSnapshot> BuildStudentSnapshotAsync(
        Student student,
        StudentProgram activeProgram,
        List<CourseRegistration> registrations,
        List<CourseRegistration> activeTermRegistrations,
        IReadOnlyDictionary<int, Course> courses,
        IReadOnlyDictionary<int, List<CurriculumCourse>> curriculumCoursesByCurriculumId,
        IReadOnlyDictionary<int, HashSet<int>> prerequisitesByCourseId,
        IReadOnlyDictionary<int, HashSet<int>> equivalentCourseIdsByCourseId,
        IDictionary<int, decimal> cgpaCache,
        IReadOnlyDictionary<int, List<ElectivePoolContext>> electivePoolsByCurriculum,
        IReadOnlyDictionary<int, decimal> electivePopularityScores)
    {
        var cgpa = await GetEffectiveCgpaAsync(student, cgpaCache);
        var ectsLimitBelow200 = await _config.GetIntAsync("Registration.EctsLimit.CgpaBelow200", 30);
        var ectsLimit200To249 = await _config.GetIntAsync("Registration.EctsLimit.Cgpa200To249", 42);
        var ectsLimitAbove250 = await _config.GetIntAsync("Registration.EctsLimit.CgpaAbove250", 48);
        var ectsLimit = RegistrationRulePolicy.GetEctsLimit(cgpa, ectsLimitBelow200, ectsLimit200To249, ectsLimitAbove250);
        var completedSemesters = Math.Max(0, student.CompletedSemesters);
        var currentSemester = completedSemesters + 1;

        var passedCourseIds = registrations
            .WherePassedGrade()
            .Select(registration => registration.CourseSection.CourseId)
            .ToHashSet();

        // Detailed failure classification: last grade per failed course
        var failedCoursesWithGrade = registrations
            .Where(r => RegistrationRulePolicy.IsFailedGrade(r.Grade))
            .GroupBy(r => r.CourseSection.CourseId)
            .Where(g => !passedCourseIds.Contains(g.Key))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.CreatedAt).First().Grade);

        var failedCourseIds = failedCoursesWithGrade.Keys.ToHashSet();

        // Split by grade type
        var retakeFFIds = failedCoursesWithGrade
            .Where(kv => kv.Value == GradeCode.FF)
            .Select(kv => kv.Key).ToHashSet();
        var retakeNAIds = failedCoursesWithGrade
            .Where(kv => kv.Value == GradeCode.NA)
            .Select(kv => kv.Key).ToHashSet();
        var retakeUIds = failedCoursesWithGrade
            .Where(kv => kv.Value == GradeCode.U)
            .Select(kv => kv.Key).ToHashSet();

        var currentTermSelectedCourseIds = activeTermRegistrations
            .Select(registration => registration.CourseSection.CourseId)
            .ToHashSet();

        var regularCourseIds = new HashSet<int>();
        var behindScheduleCourseIds = new HashSet<int>();
        var upperCourseIds = new HashSet<int>();
        var ectsLimitedCourseIds = new HashSet<int>();

        // Preserve already-selected demand first
        if (currentTermSelectedCourseIds.Count > 0)
        {
            foreach (var selectedCourseId in currentTermSelectedCourseIds)
            {
                if (failedCourseIds.Contains(selectedCourseId))
                    continue;

                var sugSem = GetSuggestedSemester(selectedCourseId, activeProgram.CurriculumId, curriculumCoursesByCurriculumId);
                if (sugSem.HasValue && sugSem.Value > currentSemester)
                    upperCourseIds.Add(selectedCourseId);
                else if (sugSem.HasValue && sugSem.Value < currentSemester)
                    behindScheduleCourseIds.Add(selectedCourseId);
                else
                    regularCourseIds.Add(selectedCourseId);
            }
        }

        var reservedEcts = SumCourseEcts(currentTermSelectedCourseIds, courses);
        reservedEcts += SumCourseEcts(failedCourseIds.Except(currentTermSelectedCourseIds), courses);
        var remainingEcts = Math.Max(0, ectsLimit - reservedEcts);

        var courseCandidates = new List<CourseCandidate>();

        var retentionThreshold = await _config.GetDecimalAsync("GPA.RetentionThreshold", 1.70m);

        if (curriculumCoursesByCurriculumId.TryGetValue(activeProgram.CurriculumId, out var curriculumCourses))
        {
            foreach (var curriculumCourse in curriculumCourses)
            {
                if (curriculumCourse.SuggestedSemester <= 0)
                    continue;

                if (!courses.TryGetValue(curriculumCourse.CourseId, out var course))
                    continue;

                if (currentTermSelectedCourseIds.Contains(course.Id) || failedCourseIds.Contains(course.Id))
                    continue;

                if (passedCourseIds.Contains(course.Id))
                    continue;

                if (!ArePrerequisitesMet(course.Id, passedCourseIds, prerequisitesByCourseId, equivalentCourseIdsByCourseId))
                    continue;

                if (RegistrationRulePolicy.HasLowCgpaRestriction(cgpa, completedSemesters, retentionThreshold)
                    && !RegistrationRulePolicy.IsAllowedUnderLowCgpaRestriction(
                        course,
                        curriculumCourse.SuggestedSemester,
                        completedSemesters,
                        activeProgram.Program?.DepartmentId,
                        false,
                        false))
                {
                    continue;
                }

                var candidate = new CourseCandidate(
                    course.Id,
                    course.Ects,
                    curriculumCourse.SuggestedSemester,
                    course.Code,
                    curriculumCourse.SuggestedSemester > currentSemester
                        ? CourseCandidateBucket.UpperCourse
                        : curriculumCourse.SuggestedSemester < currentSemester
                            ? CourseCandidateBucket.BehindSchedule
                            : CourseCandidateBucket.Regular,
                    CalculateCandidatePriority(curriculumCourse.SuggestedSemester, currentSemester, cgpa));

                courseCandidates.Add(candidate);
            }
        }

        SelectCandidatesWithinEctsLimit(
            courseCandidates,
            regularCourseIds,
            behindScheduleCourseIds,
            upperCourseIds,
            ref remainingEcts);

        // Track candidates that were eligible but couldn't fit
        foreach (var candidate in courseCandidates)
        {
            if (!regularCourseIds.Contains(candidate.CourseId)
                && !behindScheduleCourseIds.Contains(candidate.CourseId)
                && !upperCourseIds.Contains(candidate.CourseId)
                && !failedCourseIds.Contains(candidate.CourseId))
            {
                ectsLimitedCourseIds.Add(candidate.CourseId);
            }
        }

        // Clean up overlaps
        regularCourseIds.ExceptWith(failedCourseIds);
        behindScheduleCourseIds.ExceptWith(failedCourseIds);
        behindScheduleCourseIds.ExceptWith(regularCourseIds);
        upperCourseIds.ExceptWith(failedCourseIds);
        upperCourseIds.ExceptWith(regularCourseIds);
        upperCourseIds.ExceptWith(behindScheduleCourseIds);

        // ── Seçmeli dağılım ağırlıkları ──
        var electiveDemandWeights = new Dictionary<int, decimal>();
        var allSelectedCourseIds = regularCourseIds
            .Concat(behindScheduleCourseIds)
            .Concat(upperCourseIds)
            .Concat(failedCourseIds)
            .ToHashSet();

        if (electivePoolsByCurriculum.TryGetValue(activeProgram.CurriculumId, out var studentPools))
        {
            foreach (var pool in studentPools)
            {
                // Bu havuzdaki eligible dersler (zaten seçilmiş + geçilmemiş olanlar)
                var eligibleInPool = pool.CourseIdsInGroup
                    .Where(cid => allSelectedCourseIds.Contains(cid) && !passedCourseIds.Contains(cid))
                    .ToList();

                if (eligibleInPool.Count == 0) continue;

                // Geçilmiş dersler pool gereksinimini azaltır
                var alreadyPassedInPool = pool.CourseIdsInGroup.Count(cid => passedCourseIds.Contains(cid));
                var remainingRequired = Math.Max(0, pool.RequiredCourseCount - alreadyPassedInPool);
                if (remainingRequired == 0) continue;

                // Eligible dersler için popülarite skorlarını topla
                var eligiblePopSum = eligibleInPool
                    .Sum(cid => electivePopularityScores.GetValueOrDefault(cid, 1.0m / pool.CourseIdsInGroup.Count));

                foreach (var courseId in eligibleInPool)
                {
                    var popularity = electivePopularityScores.GetValueOrDefault(courseId, 1.0m / pool.CourseIdsInGroup.Count);
                    var normalizedPop = eligiblePopSum > 0 ? popularity / eligiblePopSum : 1.0m / eligibleInPool.Count;
                    var weight = Math.Min(1.0m, (decimal)remainingRequired * normalizedPop);

                    // Daha önce başka bir pool'dan ağırlık almışsa max al
                    if (electiveDemandWeights.TryGetValue(courseId, out var existing))
                        electiveDemandWeights[courseId] = Math.Max(existing, weight);
                    else
                        electiveDemandWeights[courseId] = weight;
                }
            }
        }

        return new StudentForecastSnapshot
        {
            StudentId = student.Id,
            DepartmentId = activeProgram.Program?.DepartmentId ?? 0,
            DepartmentName = activeProgram.Program?.Department?.Name ?? string.Empty,
            ProgramName = activeProgram.Program?.Name ?? string.Empty,
            RetakeCourseIds = failedCourseIds,
            RetakeFFCourseIds = retakeFFIds,
            RetakeNACourseIds = retakeNAIds,
            RetakeUCourseIds = retakeUIds,
            RegularCourseIds = regularCourseIds,
            BehindScheduleCourseIds = behindScheduleCourseIds,
            UpperCourseIds = upperCourseIds,
            EctsLimitedCourseIds = ectsLimitedCourseIds,
            ElectiveDemandWeights = electiveDemandWeights
        };
    }

    private static int? GetSuggestedSemester(
        int courseId,
        int curriculumId,
        IReadOnlyDictionary<int, List<CurriculumCourse>> curriculumCoursesByCurriculumId)
    {
        if (!curriculumCoursesByCurriculumId.TryGetValue(curriculumId, out var courses))
            return null;
        return courses.FirstOrDefault(cc => cc.CourseId == courseId)?.SuggestedSemester;
    }

    // ═══════════════════════════════════════════════════════════════
    //  DEMAND FORECAST BUILDER (enhanced)
    // ═══════════════════════════════════════════════════════════════

}
