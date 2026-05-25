using Microsoft.EntityFrameworkCore;
using SIS.Application.Grading;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public sealed class ExamRegulationResolver(AppDbContext context) : IExamRegulationResolver
{
    public async Task<ExamRegulation?> ResolveForStudentAsync(
        int studentId, int academicTermId, CancellationToken ct)
    {
        var term = await context.AcademicTerms.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == academicTermId, ct);
        if (term == null) return null;

        var program = await context.StudentPrograms.AsNoTracking()
            .Where(sp => sp.StudentId == studentId
                      && sp.Type == StudentProgramType.Main
                      && sp.IsActive)
            .Select(sp => sp.Program)
            .FirstOrDefaultAsync(ct);
        if (program == null) return null;

        return await context.ExamRegulations.AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.IsActive
                && r.ProgramId == program.Id
                && r.DegreeLevel == program.DegreeLevel
                && r.TermType == term.TermType
                && r.Year == term.Year, ct);
    }

    public async Task<WeightBounds?> ResolveCourseWeightBoundsAsync(
        int courseId, int academicTermId, CancellationToken ct)
    {
        var term = await context.AcademicTerms.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == academicTermId, ct);
        if (term == null) return null;

        var programInfos = await context.CurriculumCourses.AsNoTracking()
            .Where(cc => cc.CourseId == courseId)
            .Select(cc => new { cc.Curriculum.ProgramId, cc.Curriculum.Program.DegreeLevel })
            .Distinct()
            .ToListAsync(ct);
        if (programInfos.Count == 0) return null;

        var programIds = programInfos.Select(p => p.ProgramId).ToList();

        var regs = await context.ExamRegulations.AsNoTracking()
            .Where(r =>
                r.IsActive
                && programIds.Contains(r.ProgramId)
                && r.TermType == term.TermType
                && r.Year == term.Year)
            .ToListAsync(ct);
        if (regs.Count == 0) return null;

        // Keep only regulations whose DegreeLevel matches the program they belong to.
        var regsFiltered = regs
            .Where(r => programInfos.Any(p => p.ProgramId == r.ProgramId && p.DegreeLevel == r.DegreeLevel))
            .ToList();
        if (regsFiltered.Count == 0) return null;

        var minMid = regsFiltered.Max(r => r.MinMidtermWeightPercent);
        var maxMid = regsFiltered.Min(r => r.MaxMidtermWeightPercent);
        var minFin = regsFiltered.Max(r => r.MinFinalWeightPercent);
        var maxFin = regsFiltered.Min(r => r.MaxFinalWeightPercent);
        var empty = minMid > maxMid || minFin > maxFin;

        return new WeightBounds(minMid, maxMid, minFin, maxFin, empty);
    }
}
