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

    public async Task<IReadOnlyDictionary<int, ExamRegulation?>> ResolveForStudentsAsync(
        IReadOnlyCollection<int> studentIds, int academicTermId, CancellationToken ct)
    {
        var result = new Dictionary<int, ExamRegulation?>();
        if (studentIds.Count == 0)
            return result;

        // 1. Fetch term once
        var term = await context.AcademicTerms.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == academicTermId, ct);
        if (term == null)
        {
            foreach (var id in studentIds) result[id] = null;
            return result;
        }

        // 2. Fetch all Main+active StudentPrograms for the given students in one query
        var studentPrograms = await context.StudentPrograms.AsNoTracking()
            .Where(sp => studentIds.Contains(sp.StudentId)
                      && sp.Type == StudentProgramType.Main
                      && sp.IsActive)
            .Include(sp => sp.Program)
            .ToListAsync(ct);

        // Map studentId → program (null when no Main program found)
        var programByStudent = studentPrograms
            .GroupBy(sp => sp.StudentId)
            .ToDictionary(g => g.Key, g => g.First().Program);

        // 3. Fetch all matching active regulations for distinct programIds in one query
        var distinctProgramIds = programByStudent.Values
            .Where(p => p != null)
            .Select(p => p!.Id)
            .Distinct()
            .ToList();

        var regulations = distinctProgramIds.Count > 0
            ? await context.ExamRegulations.AsNoTracking()
                .Where(r =>
                    r.IsActive
                    && distinctProgramIds.Contains(r.ProgramId)
                    && r.TermType == term.TermType
                    && r.Year == term.Year)
                .ToListAsync(ct)
            : new List<ExamRegulation>();

        // Index regulations by programId for O(1) lookup
        var regByProgram = regulations.ToDictionary(r => r.ProgramId);

        // 4. Build result dictionary
        foreach (var studentId in studentIds)
        {
            if (!programByStudent.TryGetValue(studentId, out var program) || program == null)
            {
                result[studentId] = null;
                continue;
            }

            // Match regulation by programId AND DegreeLevel (mirrors single-student method)
            if (regByProgram.TryGetValue(program.Id, out var reg)
                && reg.DegreeLevel == program.DegreeLevel)
                result[studentId] = reg;
            else
                result[studentId] = null;
        }

        return result;
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
