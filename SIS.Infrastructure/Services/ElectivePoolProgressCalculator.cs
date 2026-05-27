using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseRegistration.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public sealed class ElectivePoolProgressCalculator : IElectivePoolProgressCalculator
{
    private readonly AppDbContext _context;
    public ElectivePoolProgressCalculator(AppDbContext context) => _context = context;

    public async Task<IReadOnlyDictionary<int, PoolProgress>> CalculateAsync(
        int studentId, IReadOnlyCollection<int> poolIds, CancellationToken ct = default)
    {
        var regs = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(cr => cr.CourseSection).ThenInclude(cs => cs.Course)
            .Where(cr => cr.StudentId == studentId
                && cr.AppliedElectivePoolId != null
                && poolIds.Contains(cr.AppliedElectivePoolId.Value))
            .ToListAsync(ct);

        var result = poolIds.ToDictionary(id => id, _ => new PoolProgress(0, 0));

        foreach (var poolGroup in regs.GroupBy(r => r.AppliedElectivePoolId!.Value))
        {
            var passed = poolGroup
                .Where(r => RegistrationRulePolicy.IsPassedGrade(r.Grade))
                .ToList();
            var count = passed.Count;
            var ects = passed.Sum(r => r.EctsSnapshot ?? r.CourseSection.Course.Ects);
            result[poolGroup.Key] = new PoolProgress(count, ects);
        }

        return result;
    }
}
