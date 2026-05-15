using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Infrastructure.Services.Scheduling.Rules;

namespace SIS.Infrastructure.Services;

/// <summary>
/// SchedulingService'in validate (manuel atama / mevcut program çakışma) bölümü.
/// Auto-schedule motoru AutoScheduleEngine'da; bu dosya manuel akış için.
/// </summary>
public partial class SchedulingService
{
    public async Task<ScheduleValidationResultDto> ValidateAsync(ScheduleValidationRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = new ScheduleValidationResultDto { IsValid = true };

        if (request.SectionId <= 0)
        {
            result.IsValid = false;
            result.Notes.Add("Geçerli bir section ID gönderilmelidir.");
            return result;
        }

        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, cancellationToken);

        if (section == null)
        {
            result.IsValid = false;
            result.Notes.Add("Section bulunamadı.");
            return result;
        }

        var snapshot = await BuildTermSnapshotAsync(section.TermId, section.Id, null, cancellationToken);
        var candidate = await BuildCandidateSnapshotAsync(section, request.InstructorId, request.ClassroomId, request.ScheduleSlots, cancellationToken);
        var softConfig = await LoadSoftConstraintConfigAsync(cancellationToken);
        var hardConfig = await LoadHardConstraintConfigAsync(cancellationToken);
        var constraintState = await _constraintEvaluation.LoadConstraintStateAsync(
            candidate.InstructorId, candidate.DepartmentId, candidate.SectionId, section.TermId, cancellationToken);

        result.Notes.Add($"Degerlendirilen donem: {snapshot.TermName}");
        result.Notes.AddRange(candidate.Notes);

        // Plan 11 Faz 5 — RuleEngine her zaman aktif (V1 hard evaluator silindi 2026-05-15).
        var runtimeSnapshot = RuntimeSnapshotAdapter.FromTermSnapshot(
            snapshot,
            curriculumConflictMode: hardConfig?.CurriculumConflictMode ?? "curriculum");
        var runtimeCandidate = RuntimeSnapshotAdapter.FromSectionSnapshot(candidate);
        var fullCfg = BuildConstraintConfig(softConfig, hardConfig);

        var ruleConflicts = await _ruleEngine.EvaluateCandidateAsync(
            runtimeCandidate, runtimeSnapshot, fullCfg, constraintState, cancellationToken);

        result.Conflicts = ruleConflicts.ToList();
        result.IsValid = ruleConflicts.Count == 0;
        return result;
    }

    public async Task<ScheduleValidationResultDto> ValidateExistingAsync(int? termId = null, CancellationToken cancellationToken = default)
    {
        var result = new ScheduleValidationResultDto { IsValid = true };
        var resolvedTerm = await ResolveTermAsync(termId, cancellationToken);

        if (resolvedTerm == null)
        {
            result.IsValid = false;
            result.Notes.Add(termId.HasValue
                ? "Belirtilen akademik dönem bulunamadı."
                : "Aktif akademik dönem bulunamadı.");
            return result;
        }

        var snapshot = await BuildTermSnapshotAsync(resolvedTerm.Id, null, null, cancellationToken);
        var evaluation = await EvaluateExistingConflictsAsync(snapshot, cancellationToken);

        evaluation.Notes.Insert(0, $"Degerlendirilen donem: {snapshot.TermName}");
        return evaluation;
    }

    public async Task<ScheduleValidationResultDto> ValidateExistingByDepartmentAsync(int departmentId, int? termId = null, CancellationToken cancellationToken = default)
    {
        var result = new ScheduleValidationResultDto { IsValid = true };
        if (departmentId <= 0)
        {
            result.IsValid = false;
            result.Notes.Add("Geçerli bir bölüm ID gönderilmelidir.");
            return result;
        }

        var resolvedTerm = await ResolveTermAsync(termId, cancellationToken);
        if (resolvedTerm == null)
        {
            result.IsValid = false;
            result.Notes.Add(termId.HasValue
                ? "Belirtilen akademik dönem bulunamadı."
                : "Aktif akademik dönem bulunamadı.");
            return result;
        }

        var snapshot = await BuildTermSnapshotAsync(resolvedTerm.Id, null, departmentId, cancellationToken);
        var evaluation = await EvaluateExistingConflictsAsync(snapshot, cancellationToken);
        evaluation.Notes.Insert(0, $"Degerlendirilen donem: {snapshot.TermName}");
        evaluation.Notes.Insert(1, $"Kapsam: {snapshot.ScopeName}");
        return evaluation;
    }

    private async Task<ScheduleValidationResultDto> EvaluateExistingConflictsAsync(TermSnapshot snapshot, CancellationToken ct)
    {
        var softConfig = await LoadSoftConstraintConfigAsync(ct);
        var hardConfig = await LoadHardConstraintConfigAsync(ct);

        if (softConfig.S4Enabled)
            snapshot.CoEnrollmentPairs = await LoadCoEnrollmentPairsAsync(snapshot.TermId, softConfig.S4MinCoPairThreshold, ct);

        var config = BuildConstraintConfig(softConfig, hardConfig);
        var state = await _constraintEvaluation.LoadConstraintStateAsync(null, null, null, snapshot.TermId, ct);

        // Plan 11 Faz 5 — Hard validation RuleEngine üzerinden; ConstraintEvaluationService
        // sadece soft note/penalty için kullanılır (V1 hard evaluator silindi 2026-05-15).
        var runtimeSnapshot = RuntimeSnapshotAdapter.FromTermSnapshot(
            snapshot, curriculumConflictMode: hardConfig?.CurriculumConflictMode ?? "curriculum");
        var hardConflicts = await _ruleEngine.EvaluateAllExistingAsync(runtimeSnapshot, config, state, ct);

        // Soft notes (V1 soft evaluator chain hâlâ aktif — Plan 12'ye ertelendi)
        var (_, softNotes) = _constraintEvaluation.EvaluateAllExisting(snapshot, config, state);

        return new ScheduleValidationResultDto
        {
            IsValid = hardConflicts.Count == 0,
            Conflicts = hardConflicts
                .OrderBy(conflict => conflict.SectionId)
                .ThenBy(conflict => conflict.ConflictType)
                .ThenBy(conflict => conflict.Day)
                .ThenBy(conflict => conflict.StartTime)
                .ToList(),
            Notes = softNotes.Distinct().OrderBy(note => note).ToList()
        };
    }

    private (List<SchedulingConflictDto> Conflicts, List<string> SoftNotes) EvaluateCandidateConflicts(
        SectionSnapshot candidate,
        TermSnapshot snapshot,
        SoftConstraintConfig softConfig,
        HardConstraintConfig? hardConfig = null,
        ConstraintState? state = null)
    {
        var config = BuildConstraintConfig(softConfig, hardConfig);
        var (conflicts, notes, _) = _constraintEvaluation.EvaluateCandidate(candidate, snapshot, config, state);
        return (conflicts, notes);
    }
}
