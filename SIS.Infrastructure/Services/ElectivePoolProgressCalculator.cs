using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public sealed class ElectivePoolProgressCalculator : IElectivePoolProgressCalculator
{
    private readonly AppDbContext _context;
    private readonly IGradeVisibilityService _gradeVisibility;

    public ElectivePoolProgressCalculator(
        AppDbContext context,
        IGradeVisibilityService? gradeVisibility = null)
    {
        _context = context;
        _gradeVisibility = gradeVisibility
            ?? new GradeVisibilityService(context, new NullWebModuleControlService());
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

        // Grade-visibility gate — IGradeVisibilityService is the single source of truth
        // (transcript, curriculum-status ve pool-progress aynı servisten okur → ikibaşlılık yok).
        // Kural: geçmiş dönem her zaman görünür, yalnızca aktif dönemin ilan edilmemiş notu gizlenir.
        // Default when TermId is absent from map = true (visible).
        var gradeVisibilityByTerm = await _gradeVisibility.BuildTermGradeVisibilityAsync(
            regs.Select(r => r.CourseSection.TermId).Distinct().ToList(), ct);

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

}
