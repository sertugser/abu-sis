using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public sealed class ElectivePoolProgressCalculator : IElectivePoolProgressCalculator
{
    private readonly AppDbContext _context;
    private readonly IWebModuleControlService _webModuleControls;

    public ElectivePoolProgressCalculator(
        AppDbContext context,
        IWebModuleControlService? webModuleControls = null)
    {
        _context = context;
        _webModuleControls = webModuleControls ?? new NullWebModuleControlService();
    }

    public async Task<IReadOnlyDictionary<int, PoolProgress>> CalculateAsync(
        int studentId,
        IReadOnlyCollection<int> poolIds,
        IReadOnlyDictionary<int, int?> poolEctsPerCourse,
        CancellationToken ct = default)
    {
        // Fix 1: Exclude Dropped — mirrors CurriculumStatusService line 68
        var regs = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(cr => cr.CourseSection).ThenInclude(cs => cs.Course)
            .Where(cr => cr.StudentId == studentId
                && cr.Status != RegistrationStatus.Dropped
                && cr.AppliedElectivePoolId != null
                && poolIds.Contains(cr.AppliedElectivePoolId.Value))
            .ToListAsync(ct);

        // Fix 2: Grade-visibility gate — mirrors CurriculumStatusService BuildGradeVisibilityMapAsync + HasVisibleLetterGrade
        // Default when TermId is absent from map = true (visible), matching GetValueOrDefault(..., true) in CurriculumStatusService line 73
        var gradeVisibilityByTerm = await BuildGradeVisibilityMapAsync(regs.Select(r => r.CourseSection.TermId));

        bool IsPassedWithVisibleGrade(SIS.Domain.Entities.CourseRegistration r) =>
            RegistrationRulePolicy.IsPassedGrade(r.Grade)
            && gradeVisibilityByTerm.GetValueOrDefault(r.CourseSection.TermId, true);

        var result = poolIds.ToDictionary(id => id, _ => new PoolProgress(0, 0));

        foreach (var poolGroup in regs.GroupBy(r => r.AppliedElectivePoolId!.Value))
        {
            var passed = poolGroup
                .Where(IsPassedWithVisibleGrade)
                .ToList();

            var count = passed.Count;

            // Fix 3: pool.EctsPerCourse middle-tier fallback — mirrors CurriculumStatusService line 316:
            // reg.EctsSnapshot ?? pool.EctsPerCourse ?? takenCourse.Ects
            var ects = passed.Sum(r =>
                r.EctsSnapshot
                ?? poolEctsPerCourse.GetValueOrDefault(poolGroup.Key)
                ?? r.CourseSection.Course.Ects);

            result[poolGroup.Key] = new PoolProgress(count, ects);
        }

        return result;
    }

    // Mirrors CurriculumStatusService.BuildGradeVisibilityMapAsync + CanStudentSeeLetterGradeAsync exactly
    private async Task<Dictionary<int, bool>> BuildGradeVisibilityMapAsync(IEnumerable<int> termIds)
    {
        var result = new Dictionary<int, bool>();
        foreach (var termId in termIds.Distinct())
        {
            result[termId] = await CanStudentSeeLetterGradeAsync(termId);
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
}
