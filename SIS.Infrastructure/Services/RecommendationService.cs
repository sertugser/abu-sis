using Microsoft.EntityFrameworkCore;
using SIS.Application.AdvisorConstraints.Services;
using SIS.Application.Recommendation.DTOs;
using SIS.Application.Recommendation.Services;
using SIS.Application.StudentEligibility.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class RecommendationService : IRecommendationService
{
    private readonly AppDbContext _context;
    private readonly IStudentEligibilityService _eligibilityService;
    private readonly ICourseDifficultyService _courseDifficultyService;
    private readonly ICriticalPathService _criticalPathService;
    private readonly IAdvisorConstraintService _advisorConstraintService;

    public RecommendationService(
        AppDbContext context,
        IStudentEligibilityService eligibilityService,
        ICourseDifficultyService courseDifficultyService,
        ICriticalPathService criticalPathService,
        IAdvisorConstraintService advisorConstraintService)
    {
        _context = context;
        _eligibilityService = eligibilityService;
        _courseDifficultyService = courseDifficultyService;
        _criticalPathService = criticalPathService;
        _advisorConstraintService = advisorConstraintService;
    }

    public async Task<StudentRecommendationsDto> GetStudentRecommendationsAsync(
        int studentId,
        int pacePreference = 50,
        CancellationToken cancellationToken = default)
    {
        pacePreference = Math.Clamp(pacePreference, 0, 100);

        var activeTermId = await _context.AcademicTerms
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var eligibleCourses = await _eligibilityService.GetStudentEligibleCoursesAsync(studentId, cancellationToken);
        var recommendations = new List<CourseRecommendationDto>();

        var constraints = activeTermId.HasValue
            ? await _advisorConstraintService.GetEffectiveConstraintsAsync(
                studentId,
                eligibleCourses.Where(c => c.IsEligible).Select(c => c.CourseId),
                activeTermId.Value,
                cancellationToken)
            : new Dictionary<int, SIS.Application.AdvisorConstraints.DTOs.EffectiveAdvisorConstraintDto>();

        foreach (var item in eligibleCourses.Where(c => c.IsEligible).OrderBy(c => c.CourseCode))
        {
            if (constraints.TryGetValue(item.CourseId, out var effectiveConstraint)
                && effectiveConstraint.ConstraintType == SIS.Domain.Enums.AdvisorConstraintType.Block)
            {
                continue;
            }

            var difficulty = await _courseDifficultyService.GetDifficultyAsync(item.CourseId, cancellationToken);
            var criticalPath = await _criticalPathService.AnalyzeCourseAsync(studentId, item.CourseId, cancellationToken);
            var reasons = BuildReasons(item, criticalPath, effectiveConstraint);
            var priorityScore = CalculatePriorityScore(item, difficulty, criticalPath, pacePreference, effectiveConstraint);
            var category = DetermineCategory(item, criticalPath, priorityScore);

            recommendations.Add(new CourseRecommendationDto
            {
                CourseId = item.CourseId,
                CourseCode = item.CourseCode,
                CourseName = item.CourseName,
                Ects = item.Ects,
                IsCompulsory = item.IsCompulsory,
                SuggestedSemester = item.SuggestedSemester,
                TakeType = item.TakeType,
                Category = category,
                PriorityScore = priorityScore,
                Difficulty = difficulty,
                CriticalPath = criticalPath,
                PreviousGrade = item.PreviousGrade,
                AdvisorConstraintType = effectiveConstraint?.ConstraintType,
                AdvisorConstraintNote = effectiveConstraint?.Note,
                Reasons = reasons
            });
        }

        recommendations = recommendations
            .OrderBy(r => CategoryOrder(r.Category))
            .ThenByDescending(r => r.PriorityScore)
            .ThenBy(r => r.CourseCode)
            .ToList();

        return new StudentRecommendationsDto
        {
            StudentId = studentId,
            PacePreference = pacePreference,
            GeneratedAtUtc = DateTime.UtcNow,
            Recommendations = recommendations
        };
    }

    private static List<string> BuildReasons(
        SIS.Application.StudentEligibility.DTOs.StudentCourseEligibilityDto item,
        CriticalPathAnalysisDto criticalPath,
        SIS.Application.AdvisorConstraints.DTOs.EffectiveAdvisorConstraintDto? effectiveConstraint)
    {
        var reasons = new List<string>();

        if (item.TakeType == "MustRetake" && item.PreviousGrade != null)
            reasons.Add($"{item.PreviousGrade} aldin");
        else if (item.TakeType == "BehindSchedule")
            reasons.Add("mufredat sirasinin gerisindesin");
        else if (item.TakeType == "UpperCourse")
            reasons.Add("ust donem dersi olmasina ragmen on kosullar tamam");
        else
            reasons.Add("on kosullar tamam");

        if (criticalPath.EstimatedDelaySemesters > 0)
            reasons.Add($"bu ders alinmazsa mezuniyet {criticalPath.EstimatedDelaySemesters} donem uzayabilir");

        if (effectiveConstraint?.ConstraintType == SIS.Domain.Enums.AdvisorConstraintType.Warning)
        {
            reasons.Add(string.IsNullOrWhiteSpace(effectiveConstraint.Note)
                ? "danismaniniz bu dersi almanizi onermemektedir"
                : $"danisman uyarisi: {effectiveConstraint.Note}");
        }

        return reasons;
    }

    private static decimal CalculatePriorityScore(
        SIS.Application.StudentEligibility.DTOs.StudentCourseEligibilityDto item,
        CourseDifficultyDto difficulty,
        CriticalPathAnalysisDto criticalPath,
        int pacePreference,
        SIS.Application.AdvisorConstraints.DTOs.EffectiveAdvisorConstraintDto? effectiveConstraint)
    {
        var paceWeight = pacePreference / 100m;
        decimal score = item.TakeType switch
        {
            "MustRetake" => 92m,
            "BehindSchedule" => 70m,
            "Regular" => item.IsCompulsory ? 58m : 42m,
            "UpperCourse" => 28m + (paceWeight * 18m),
            _ => 40m
        };

        score += Math.Min(criticalPath.EstimatedDelaySemesters * 12m, 30m);
        if (item.IsCompulsory)
            score += 8m;

        score += (100m - difficulty.Score) * paceWeight * 0.10m;
        score -= difficulty.Score * (1m - paceWeight) * 0.10m;

        if (effectiveConstraint?.ConstraintType == SIS.Domain.Enums.AdvisorConstraintType.Warning)
            score -= 5m;

        return decimal.Round(Math.Clamp(score, 0m, 100m), 2, MidpointRounding.AwayFromZero);
    }

    private static string DetermineCategory(
        SIS.Application.StudentEligibility.DTOs.StudentCourseEligibilityDto item,
        CriticalPathAnalysisDto criticalPath,
        decimal priorityScore)
    {
        if (item.TakeType == "MustRetake"
            || criticalPath.EstimatedDelaySemesters >= 2
            || (item.IsCompulsory && item.SuggestedSemester > 0 && item.SuggestedSemester <= item.CurrentSemester))
        {
            return "Zorunlu Al";
        }

        if (priorityScore >= 55m
            || criticalPath.EstimatedDelaySemesters == 1
            || item.TakeType == "BehindSchedule"
            || item.IsCompulsory)
        {
            return "Onerilen";
        }

        return "Alabilirsin";
    }

    private static int CategoryOrder(string category) => category switch
    {
        "Zorunlu Al" => 0,
        "Onerilen" => 1,
        _ => 2
    };
}
