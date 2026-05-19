using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Constraints;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling;

/// <summary>
/// Plan 12 Faz 7 — V1 <c>ConstraintEvaluationService.LoadConstraintStateAsync</c> portu.
/// ConstraintDefinitions + ilgili ConstraintExceptions DB'den çekilir; RuleEngine
/// disable/exempt/category/weight kararlarını bu state üzerinden alır.
/// </summary>
public sealed class ConstraintStateLoader : IConstraintStateLoader
{
    private readonly AppDbContext _context;

    public ConstraintStateLoader(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ConstraintState> LoadConstraintStateAsync(
        int? instructorId, int? departmentId, int? sectionId, int? termId,
        CancellationToken cancellationToken)
    {
        var definitions = await _context.ConstraintDefinitions
            .AsNoTracking()
            .ToDictionaryAsync(d => d.Code, cancellationToken);

        var exceptions = await _context.ConstraintExceptions
            .AsNoTracking()
            .Include(e => e.ConstraintDefinition)
            .Where(e =>
                (e.InstructorId == null || e.InstructorId == instructorId) &&
                (e.DepartmentId == null || e.DepartmentId == departmentId) &&
                (e.CourseSectionId == null || e.CourseSectionId == sectionId) &&
                (e.TermId == null || e.TermId == termId))
            .ToListAsync(cancellationToken);

        return new ConstraintState(definitions, exceptions);
    }
}
