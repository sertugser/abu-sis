using Microsoft.EntityFrameworkCore;
using SIS.Application.Recommendation.DTOs;
using SIS.Application.Recommendation.Services;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class CourseDifficultyService : ICourseDifficultyService
{
    private readonly AppDbContext _context;

    public CourseDifficultyService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CourseDifficultyDto> GetDifficultyAsync(int courseId, CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);

        if (course == null)
            return new CourseDifficultyDto { CourseId = courseId };

        var graded = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(r =>
                r.CourseSection.CourseId == courseId
                && r.Grade != null
                && r.Status != RegistrationStatus.Draft
                && r.Status != RegistrationStatus.Dropped)
            .Select(r => new DifficultyRegistrationSnapshot
            {
                StudentId = r.StudentId,
                Grade = r.Grade!.Value,
                TermId = r.CourseSection.TermId,
                TermEnd = r.CourseSection.Term.TermEnd
            })
            .ToListAsync(cancellationToken);

        var lastThreeTermIds = graded
            .OrderByDescending(r => r.TermEnd)
            .Select(r => r.TermId)
            .Distinct()
            .Take(3)
            .ToHashSet();

        var lastFourTermGroups = graded
            .Select(r => new { r.TermId, r.TermEnd })
            .Distinct()
            .OrderByDescending(r => r.TermEnd)
            .Take(4)
            .Select(r => r.TermId)
            .ToHashSet();

        var recent = graded
            .Where(r => lastThreeTermIds.Contains(r.TermId))
            .ToList();

        var failureRate = recent.Count == 0
            ? 0m
            : (decimal)recent.Count(r => r.Grade is GradeCode.FF or GradeCode.NA or GradeCode.U) / recent.Count;
        var failureRateComponent = DecimalRound(failureRate * 100m);

        var gradePoints = recent
            .Select(r => GradePointMap.GetPoint(r.Grade))
            .Where(p => p.HasValue)
            .Select(p => p!.Value)
            .ToList();

        decimal gradeDistributionComponent = 50m;
        if (gradePoints.Count > 0)
        {
            var average = gradePoints.Average();
            var variance = gradePoints
                .Select(point => Math.Pow((double)(point - average), 2))
                .Average();
            var stdDev = (decimal)Math.Sqrt(variance);
            var normalizedStdDev = Math.Min(stdDev / 2m, 1m);
            var normalizedAveragePenalty = Math.Min((4m - average) / 4m, 1m);
            gradeDistributionComponent = DecimalRound(Math.Clamp((normalizedStdDev * 50m) + (normalizedAveragePenalty * 50m), 0m, 100m));
        }

        var repeatGroups = graded
            .GroupBy(r => r.StudentId)
            .Where(g => g.Count() >= 2)
            .ToList();

        decimal retakeDifficultyComponent = 50m;
        if (repeatGroups.Count > 0)
        {
            var successfulRepeatCount = repeatGroups.Count(g =>
            {
                var latestGrade = g.OrderByDescending(x => x.TermEnd).First().Grade;
                return GradePointMap.IsGpaIncluded(latestGrade) && GradePointMap.GetPoint(latestGrade) > 0m
                    || latestGrade is GradeCode.S or GradeCode.CC or GradeCode.CB or GradeCode.BB or GradeCode.BA or GradeCode.AA or GradeCode.DC or GradeCode.DD;
            });

            var successRate = (decimal)successfulRepeatCount / repeatGroups.Count;
            retakeDifficultyComponent = DecimalRound(Math.Clamp(100m - (successRate * 100m), 0m, 100m));
        }

        var ectsComponent = DecimalRound(Math.Clamp(course.Ects / 8m * 100m, 0m, 100m));

        decimal trendComponent = 50m;
        var termAverages = graded
            .Where(r => lastFourTermGroups.Contains(r.TermId))
            .Select(r => new { r.TermId, r.TermEnd, Point = GradePointMap.GetPoint(r.Grade) })
            .Where(r => r.Point.HasValue)
            .GroupBy(r => new { r.TermId, r.TermEnd })
            .OrderBy(g => g.Key.TermEnd)
            .Select(g => g.Average(x => x.Point!.Value))
            .ToList();

        if (termAverages.Count >= 2)
        {
            var trendDelta = termAverages.First() - termAverages.Last();
            trendComponent = DecimalRound(Math.Clamp(50m + (trendDelta * 20m), 0m, 100m));
        }

        var totalScore = DecimalRound(
            (failureRateComponent * 0.30m)
            + (gradeDistributionComponent * 0.25m)
            + (retakeDifficultyComponent * 0.20m)
            + (ectsComponent * 0.15m)
            + (trendComponent * 0.10m));

        return new CourseDifficultyDto
        {
            CourseId = courseId,
            Score = totalScore,
            FailureRateComponent = failureRateComponent,
            GradeDistributionComponent = gradeDistributionComponent,
            RetakeDifficultyComponent = retakeDifficultyComponent,
            EctsComponent = ectsComponent,
            TrendComponent = trendComponent
        };
    }

    private static decimal DecimalRound(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed class DifficultyRegistrationSnapshot
    {
        public int StudentId { get; init; }
        public GradeCode Grade { get; init; }
        public int TermId { get; init; }
        public DateTime TermEnd { get; init; }
    }
}
