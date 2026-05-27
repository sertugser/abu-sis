using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.DTOs;
using SIS.Application.Common.Services;
using SIS.Application.StudentCard.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Öğrencinin transkript (transcript) belgesini kuran servis.
/// Logic <c>StudentController.GetTranscript</c>'tan birebir taşındı; admin endpoint'lerce
/// yeniden kullanılabilmesi için <c>studentId</c> ile anahtarlanır.
/// </summary>
public class StudentTranscriptService : IStudentTranscriptService
{
    private readonly AppDbContext _context;
    private readonly IWebModuleControlService _webModuleControls;

    public StudentTranscriptService(
        AppDbContext context,
        IWebModuleControlService? webModuleControls = null)
    {
        _context = context;
        _webModuleControls = webModuleControls ?? new NullWebModuleControlService();
    }

    public async Task<StudentTranscriptDto?> GetTranscriptAsync(int studentId, CancellationToken ct = default)
    {
        var student = await _context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.Faculty)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student == null)
            return null;

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == student.UserId, ct);

        if (user == null)
            return null;

        var activeProgram = student.StudentPrograms.FirstOrDefault(sp => sp.IsActive);
        if (activeProgram == null)
            return null;

        var registrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
            .Where(cr => cr.StudentId == student.Id
                && cr.Grade != null
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Dropped)
            .ToListAsync(ct);

        var transcriptGradeVisibility = await BuildGradeVisibilityMapAsync(registrations.Select(r => r.CourseSection.TermId));
        registrations = registrations
            .Where(registration => transcriptGradeVisibility.GetValueOrDefault(registration.CourseSection.TermId, true))
            .ToList();

        var registrationIds = registrations.Select(registration => registration.Id).ToList();
        var courseIds = registrations
            .Select(registration => registration.CourseSection.CourseId)
            .Distinct()
            .ToList();
        var termIds = registrations
            .Select(registration => registration.CourseSection.TermId)
            .Distinct()
            .ToList();
        var equivalentCourseIdsByCourseId = courseIds.Count == 0
            ? new Dictionary<int, HashSet<int>>()
            : (await _context.CourseEquivalencies
                .AsNoTracking()
                .Where(equivalency => courseIds.Contains(equivalency.CourseId))
                .ToListAsync(ct))
                .GroupBy(equivalency => equivalency.CourseId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(equivalency => equivalency.EquivalentCourseId).ToHashSet());
        var courseChainIdByCourseId = BuildCourseChainIdMap(courseIds, equivalentCourseIdsByCourseId);
        var registrationMetadataById = registrations.ToDictionary(
            registration => registration.Id,
            registration => (registration.CourseSection.CourseId, registration.CourseSection.TermId));
        var gradeComponentStatsByCourseTerm = courseIds.Count == 0 || termIds.Count == 0
            ? new Dictionary<(int CourseId, int TermId), (decimal TotalWeight, int ComponentCount)>()
            : (await _context.GradeComponents
                .AsNoTracking()
                .Where(component => courseIds.Contains(component.CourseId) && termIds.Contains(component.AcademicTermId))
                .Select(component => new
                {
                    component.CourseId,
                    TermId = component.AcademicTermId,
                    component.Id,
                    component.Weight
                })
                .ToListAsync(ct))
                .GroupBy(component => (component.CourseId, component.TermId))
                .ToDictionary(
                    group => group.Key,
                    group => (
                        TotalWeight: group.Sum(component => component.Weight),
                        ComponentCount: group.Select(component => component.Id).Distinct().Count()));
        var weightedAverageMap = registrationIds.Count == 0
            ? new Dictionary<int, decimal?>()
            : (await _context.StudentGrades
                .AsNoTracking()
                .Include(grade => grade.GradeComponent)
                .Where(grade => registrationIds.Contains(grade.CourseRegistrationId) && grade.Score != null)
                .ToListAsync(ct))
                .GroupBy(grade => grade.CourseRegistrationId)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        if (!registrationMetadataById.TryGetValue(group.Key, out var registrationMetadata))
                            return (decimal?)null;

                        if (!gradeComponentStatsByCourseTerm.TryGetValue(
                                (registrationMetadata.CourseId, registrationMetadata.TermId),
                                out var componentStats)
                            || componentStats.TotalWeight <= 0
                            || componentStats.ComponentCount <= 0)
                            return (decimal?)null;

                        var gradedComponents = group
                            .Where(item => item.GradeComponent != null)
                            .GroupBy(item => item.GradeComponentId)
                            .Select(componentGroup =>
                            {
                                var grade = componentGroup.First();
                                return new
                                {
                                    Score = grade.Score!.Value,
                                    grade.GradeComponent.Weight
                                };
                            })
                            .ToList();

                        var actualWeight = gradedComponents.Sum(item => item.Weight);
                        if (gradedComponents.Count != componentStats.ComponentCount || actualWeight != componentStats.TotalWeight)
                            return (decimal?)null;

                        var totalWeightedScore = gradedComponents.Sum(item => item.Score * item.Weight / 100m);
                        return Math.Round(
                            totalWeightedScore * 100m / componentStats.TotalWeight,
                            2,
                            MidpointRounding.AwayFromZero);
                    });

        var repeatedChainCounts = registrations
            .GroupBy(registration => courseChainIdByCourseId.GetValueOrDefault(registration.CourseSection.CourseId, registration.CourseSection.CourseId))
            .ToDictionary(group => group.Key, group => group.Count());
        var latestGradesByCourse = new Dictionary<int, CourseRegistration>();
        var termGroups = registrations
            .GroupBy(cr => cr.CourseSection.TermId)
            .Select(group => new
            {
                Term = group.First().CourseSection.Term,
                Registrations = group
                    .OrderBy(cr => cr.CourseSection.Course.Code)
                    .ThenBy(cr => cr.Id)
                    .ToList()
            })
            .OrderBy(group => group.Term.TermStart)
            .ThenBy(group => group.Term.Id)
            .ToList();

        var transcriptTerms = new List<StudentTranscriptTermDto>(termGroups.Count);

        foreach (var termGroup in termGroups)
        {
            foreach (var registration in termGroup.Registrations
                .OrderBy(registration => registration.Id))
            {
                var chainId = courseChainIdByCourseId.GetValueOrDefault(
                    registration.CourseSection.CourseId,
                    registration.CourseSection.CourseId);
                latestGradesByCourse[chainId] = registration;
            }

            var courseDtos = termGroup.Registrations
                .Select(registration =>
                {
                    var course = registration.CourseSection.Course;
                    var chainId = courseChainIdByCourseId.GetValueOrDefault(course.Id, course.Id);
                    var credits = course.TheoryHours + course.LabHours;
                    var grade = registration.Grade!.Value;
                    var gradePoint = GradePointMap.GetPoint(grade);
                    var weightedEcts = gradePoint.HasValue
                        ? Math.Round(course.Ects * gradePoint.Value, 2, MidpointRounding.AwayFromZero)
                        : (decimal?)null;

                    return new StudentTranscriptCourseDto
                    {
                        RegistrationId = registration.Id,
                        CourseId = course.Id,
                        CourseCode = course.Code,
                        CourseName = course.Name,
                        Credits = credits,
                        Ects = course.Ects,
                        EquivalentCourseIds = equivalentCourseIdsByCourseId.TryGetValue(course.Id, out var equivalentIds)
                            ? equivalentIds.OrderBy(id => id).ToList()
                            : new List<int>(),
                        AverageScore = weightedAverageMap.GetValueOrDefault(registration.Id),
                        Grade = grade.ToString(),
                        Status = registration.Status.ToString(),
                        IsRepeated = registration.IsRepeated || repeatedChainCounts.GetValueOrDefault(chainId) > 1,
                        IsPassed = IsPassedGrade(grade),
                        CountsTowardsGpa = gradePoint.HasValue,
                        GradePoint = gradePoint,
                        WeightedEcts = weightedEcts
                    };
                })
                .ToList();

            var attemptedCredits = courseDtos.Sum(course => course.Credits);
            var attemptedEcts = courseDtos.Sum(course => course.Ects);
            var earnedCredits = courseDtos.Where(course => course.IsPassed).Sum(course => course.Credits);
            var earnedEcts = courseDtos.Where(course => course.IsPassed).Sum(course => course.Ects);
            var gpaEcts = courseDtos.Where(course => course.CountsTowardsGpa).Sum(course => course.Ects);
            var gpaPoints = courseDtos.Sum(course => course.WeightedEcts ?? 0m);

            var cumulativeRegistrations = latestGradesByCourse.Values.ToList();
            var cumulativeGpaEcts = cumulativeRegistrations.Sum(GetGpaEcts);
            var cumulativeGpaPoints = cumulativeRegistrations.Sum(GetWeightedEcts);

            transcriptTerms.Add(new StudentTranscriptTermDto
            {
                TermId = termGroup.Term.Id,
                TermName = termGroup.Term.Name,
                Year = termGroup.Term.Year,
                Semester = termGroup.Term.TermType.ToString(),
                AttemptedCredits = attemptedCredits,
                AttemptedEcts = attemptedEcts,
                EarnedCredits = earnedCredits,
                EarnedEcts = earnedEcts,
                GpaEcts = gpaEcts,
                GpaPoints = Math.Round(gpaPoints, 2, MidpointRounding.AwayFromZero),
                CumulativeGpaEcts = cumulativeGpaEcts,
                CumulativeGpaPoints = Math.Round(cumulativeGpaPoints, 2, MidpointRounding.AwayFromZero),
                Sgpa = CalculateAverage(termGroup.Registrations),
                Cgpa = CalculateAverage(cumulativeRegistrations),
                Courses = courseDtos
            });
        }

        var latestRegistrations = latestGradesByCourse.Values.ToList();
        var totalGpaEcts = latestRegistrations.Sum(GetGpaEcts);
        var totalGpaPoints = latestRegistrations.Sum(GetWeightedEcts);

        var transcript = new StudentTranscriptDto
        {
            StudentId = student.Id,
            StudentNumber = student.StudentNumber,
            FullName = $"{user.FirstName} {user.LastName}",
            FacultyName = activeProgram.Program?.Department?.Faculty?.Name ?? string.Empty,
            DepartmentName = activeProgram.Program?.Department?.Name ?? string.Empty,
            ProgramName = activeProgram.Program?.Name ?? string.Empty,
            StudentStatus = student.Status.ToString(),
            EnrollmentDate = activeProgram.EnrollmentDate,
            GeneratedAt = DateTime.UtcNow,
            CurrentCgpa = transcriptTerms.Count > 0 ? transcriptTerms[^1].Cgpa : student.Cgpa,
            TotalAttemptedCredits = latestRegistrations.Sum(reg => reg.CourseSection.Course.TheoryHours + reg.CourseSection.Course.LabHours),
            TotalAttemptedEcts = latestRegistrations.Sum(reg => reg.CourseSection.Course.Ects),
            TotalEarnedCredits = latestRegistrations.Where(reg => IsPassedGrade(reg.Grade!.Value)).Sum(reg => reg.CourseSection.Course.TheoryHours + reg.CourseSection.Course.LabHours),
            TotalEarnedEcts = latestRegistrations.Where(reg => IsPassedGrade(reg.Grade!.Value)).Sum(reg => reg.CourseSection.Course.Ects),
            TotalGpaEcts = totalGpaEcts,
            TotalGpaPoints = Math.Round(totalGpaPoints, 2, MidpointRounding.AwayFromZero),
            Terms = transcriptTerms
        };

        return transcript;
    }

    private async Task<Dictionary<int, bool>> BuildGradeVisibilityMapAsync(IEnumerable<int> termIds)
    {
        // Yalnızca AKTİF dönemin notları "ilan" durumuna bağlıdır: aktif dönemde
        // kayıtlı olunan, henüz ilan edilmemiş dersler gizlenir. Geçmiş (sonuçlanmış)
        // dönemlerin notları transkriptte her zaman görünür — hem öğrenci hem yönetici.
        var activeTermId = await _context.AcademicTerms
            .AsNoTracking()
            .ActiveTerm()
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync();

        var result = new Dictionary<int, bool>();
        foreach (var termId in termIds.Distinct())
        {
            result[termId] = termId != activeTermId
                || await CanStudentSeeLetterGradeAsync(termId);
        }

        return result;
    }

    private async Task<bool> CanStudentSeeLetterGradeAsync(int termId)
    {
        var finalAccess = await _webModuleControls.EvaluateAsync("letter-grade-publication", termId);
        if (finalAccess.IsOpen)
            return true;

        var makeupAccess = await _webModuleControls.EvaluateAsync("makeup-letter-grade-publication", termId);
        return makeupAccess.IsOpen;
    }

    private static bool IsPassedGrade(GradeCode grade) =>
        grade != GradeCode.FF
        && grade != GradeCode.NA
        && grade != GradeCode.U
        && grade != GradeCode.W
        && grade != GradeCode.I;

    private static int GetGpaEcts(CourseRegistration registration) =>
        GradePointMap.IsGpaIncluded(registration.Grade!.Value)
            ? registration.CourseSection.Course.Ects
            : 0;

    private static decimal GetWeightedEcts(CourseRegistration registration)
    {
        var point = GradePointMap.GetPoint(registration.Grade!.Value);
        return point.HasValue
            ? Math.Round(registration.CourseSection.Course.Ects * point.Value, 2, MidpointRounding.AwayFromZero)
            : 0m;
    }

    private static decimal CalculateAverage(IEnumerable<CourseRegistration> registrations)
    {
        var totalEcts = 0;
        decimal totalPoints = 0;

        foreach (var registration in registrations)
        {
            var point = GradePointMap.GetPoint(registration.Grade!.Value);
            if (!point.HasValue)
                continue;

            var ects = registration.CourseSection.Course.Ects;
            if (ects == 0)
                continue;

            totalEcts += ects;
            totalPoints += ects * point.Value;
        }

        if (totalEcts == 0)
            return 0m;

        return Math.Round(totalPoints / totalEcts, 2, MidpointRounding.AwayFromZero);
    }

    private static Dictionary<int, int> BuildCourseChainIdMap(
        IEnumerable<int> courseIds,
        IReadOnlyDictionary<int, HashSet<int>> equivalentCourseIdsByCourseId)
    {
        var distinctCourseIds = courseIds.Distinct().ToList();
        var map = distinctCourseIds.ToDictionary(courseId => courseId, courseId => courseId);
        var relevantCourseIds = distinctCourseIds.ToHashSet();
        var visited = new HashSet<int>();

        foreach (var courseId in distinctCourseIds)
        {
            if (!visited.Add(courseId))
                continue;

            var queue = new Queue<int>();
            var component = new List<int>();
            queue.Enqueue(courseId);

            while (queue.Count > 0)
            {
                var currentCourseId = queue.Dequeue();
                component.Add(currentCourseId);

                if (!equivalentCourseIdsByCourseId.TryGetValue(currentCourseId, out var equivalentIds))
                    continue;

                foreach (var equivalentCourseId in equivalentIds)
                {
                    if (!relevantCourseIds.Contains(equivalentCourseId) || !visited.Add(equivalentCourseId))
                        continue;

                    queue.Enqueue(equivalentCourseId);
                }
            }

            var chainId = component.Min();
            foreach (var componentCourseId in component)
            {
                map[componentCourseId] = chainId;
            }
        }

        return map;
    }
}
