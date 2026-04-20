using Microsoft.EntityFrameworkCore;
using SIS.Application.Recommendation.DTOs;
using SIS.Application.Recommendation.Services;
using SIS.Application.CourseRegistration.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class CriticalPathService : ICriticalPathService
{
    private readonly AppDbContext _context;

    public CriticalPathService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CriticalPathAnalysisDto> AnalyzeCourseAsync(
        int studentId,
        int courseId,
        CancellationToken cancellationToken = default)
    {
        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentPrograms)
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);

        if (student == null)
            return new CriticalPathAnalysisDto { CourseId = courseId };

        var mainProgram = student.StudentPrograms
            .FirstOrDefault(sp => sp.Type == SIS.Domain.Enums.StudentProgramType.Main && sp.IsActive);

        if (mainProgram == null)
            return new CriticalPathAnalysisDto { CourseId = courseId };

        var curriculumCourses = await _context.CurriculumCourses
            .AsNoTracking()
            .Include(cc => cc.Course)
            .Where(cc => cc.CurriculumId == mainProgram.CurriculumId)
            .ToListAsync(cancellationToken);

        var curriculumCourseIds = curriculumCourses
            .Select(cc => cc.CourseId)
            .ToHashSet();

        var courseInCurriculum = curriculumCourses.FirstOrDefault(cc => cc.CourseId == courseId);

        var registrationSnapshots = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(cr =>
                cr.StudentId == studentId
                && cr.Grade != null)
            .Select(cr => new
            {
                cr.Grade,
                CourseId = cr.CourseSection.CourseId
            })
            .ToListAsync(cancellationToken);

        var passedSet = registrationSnapshots
            .Where(cr => cr.Grade.HasValue && RegistrationRulePolicy.IsPassedGrade(cr.Grade.Value))
            .Select(cr => cr.CourseId)
            .ToHashSet();

        if (passedSet.Contains(courseId))
            return new CriticalPathAnalysisDto { CourseId = courseId, EstimatedDelaySemesters = 0 };

        var prerequisites = await _context.CoursePrerequisites
            .AsNoTracking()
            .Where(cp => curriculumCourseIds.Contains(cp.CourseId))
            .ToListAsync(cancellationToken);

        var dependentsByCourseId = prerequisites
            .GroupBy(cp => cp.RequiredCourseId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(cp => cp.CourseId)
                    .Where(id => !passedSet.Contains(id))
                    .Distinct()
                    .ToList());

        var dependentIds = new HashSet<int>();
        var visiting = new HashSet<int>();
        var longestPath = GetLongestDependentChain(courseId, dependentsByCourseId, dependentIds, visiting);
        var baseDelay = courseInCurriculum?.IsCompulsory == true ? 1 : 0;
        var delay = Math.Max(baseDelay, longestPath);

        var dependentCourses = curriculumCourses
            .Where(cc => dependentIds.Contains(cc.CourseId))
            .OrderBy(cc => cc.SuggestedSemester)
            .ThenBy(cc => cc.Course.Code)
            .Select(cc => $"{cc.Course.Code} {cc.Course.Name}")
            .ToList();

        return new CriticalPathAnalysisDto
        {
            CourseId = courseId,
            EstimatedDelaySemesters = delay,
            DependentCourses = dependentCourses
        };
    }

    private static int GetLongestDependentChain(
        int courseId,
        IReadOnlyDictionary<int, List<int>> dependentsByCourseId,
        ISet<int> dependentIds,
        ISet<int> visiting)
    {
        if (!dependentsByCourseId.TryGetValue(courseId, out var dependents) || dependents.Count == 0)
            return 0;

        if (!visiting.Add(courseId))
            return 0;

        var longest = 0;
        foreach (var dependentId in dependents)
        {
            dependentIds.Add(dependentId);
            longest = Math.Max(longest, 1 + GetLongestDependentChain(dependentId, dependentsByCourseId, dependentIds, visiting));
        }

        visiting.Remove(courseId);
        return longest;
    }
}
