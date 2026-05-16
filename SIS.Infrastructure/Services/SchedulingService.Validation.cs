using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Rules;

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

        var (candidate, candidateNotes) = await BuildCandidateRuntimeAsync(
            section, request.InstructorId, request.ClassroomId, request.ScheduleSlots, cancellationToken);
        var softConfig = await LoadSoftConstraintConfigAsync(cancellationToken);
        var hardConfig = await LoadHardConstraintConfigAsync(cancellationToken);
        var constraintState = await _constraintStateLoader.LoadConstraintStateAsync(
            candidate.InstructorId, candidate.DepartmentId, candidate.SectionId, section.TermId, cancellationToken);

        var runtimeSnapshot = await BuildRuntimeSnapshotAsync(
            section.TermId, section.Id, null, cancellationToken);

        result.Notes.Add($"Degerlendirilen donem: {runtimeSnapshot.TermName}");
        result.Notes.AddRange(candidateNotes);

        var fullCfg = BuildConstraintConfig(softConfig, hardConfig);

        var ruleConflicts = await _ruleEngine.EvaluateCandidateAsync(
            candidate, runtimeSnapshot, fullCfg, constraintState, cancellationToken);

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

        var snapshot = await BuildRuntimeSnapshotAsync(resolvedTerm.Id, null, null, cancellationToken);
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

        var snapshot = await BuildRuntimeSnapshotAsync(resolvedTerm.Id, null, departmentId, cancellationToken);
        var evaluation = await EvaluateExistingConflictsAsync(snapshot, cancellationToken);
        evaluation.Notes.Insert(0, $"Degerlendirilen donem: {snapshot.TermName}");
        evaluation.Notes.Insert(1, $"Kapsam: {snapshot.ScopeName}");
        return evaluation;
    }

    private async Task<ScheduleValidationResultDto> EvaluateExistingConflictsAsync(RuntimeSnapshot snapshot, CancellationToken ct)
    {
        var softConfig = await LoadSoftConstraintConfigAsync(ct);
        var hardConfig = await LoadHardConstraintConfigAsync(ct);
        var config = BuildConstraintConfig(softConfig, hardConfig);
        var state = await _constraintStateLoader.LoadConstraintStateAsync(null, null, null, snapshot.TermId, ct);

        // Plan 12 Faz 5 — Hard + soft validation RuleEngine üzerinden. Soft note'lar
        // EvaluateWithBreakdownAsync ConstraintScoreItemDto.SampleViolations'tan toplanır.
        var hardConflicts = await _ruleEngine.EvaluateAllExistingAsync(snapshot, config, state, ct);
        var breakdown = await _ruleEngine.EvaluateWithBreakdownAsync(snapshot, config, state, ct);

        var softNotes = breakdown.Constraints
            .Where(item => item.Category == "Soft")
            .SelectMany(item => item.SampleViolations)
            .ToList();

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

    private async Task<(List<SchedulingConflictDto> Conflicts, List<string> SoftNotes)> EvaluateCandidateConflictsAsync(
        RuntimeCandidate candidate,
        RuntimeSnapshot snapshot,
        SoftConstraintConfig softConfig,
        HardConstraintConfig? hardConfig,
        ConstraintState? state,
        CancellationToken ct)
    {
        // Plan 12 Faz 7 — V1 ConstraintEvaluationService.EvaluateCandidate yerine RuleEngine
        // delegasyonu. SuggestAsync path'i için kullanılır (manuel slot önerisi).
        var config = BuildConstraintConfig(softConfig, hardConfig);

        var conflicts = (await _ruleEngine.EvaluateCandidateAsync(candidate, snapshot, config, state, ct)).ToList();

        // Soft notes — SuggestAsync feasibility için sadece bilgilendirici, ihlal değil.
        // CalculatePenaltyAsync skalar dönüyor; tek candidate için detaylı breakdown
        // SuggestAsync hızı için skipped (UI'da kabul edilebilir trade).
        return (conflicts, new List<string>());
    }
}
