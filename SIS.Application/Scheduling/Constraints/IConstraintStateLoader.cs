namespace SIS.Application.Scheduling.Constraints;

/// <summary>
/// Plan 12 Faz 7 — V1 <c>ConstraintEvaluationService.LoadConstraintStateAsync</c>'i ayrı
/// bir servise extract eder. ConstraintEvaluationService silindiğinde state loader bağımsız
/// kalır ve RuleEngine'a feed eder. SchedulingService, SimulationService gibi caller'lar
/// bunu inject eder.
/// </summary>
public interface IConstraintStateLoader
{
    Task<ConstraintState> LoadConstraintStateAsync(
        int? instructorId, int? departmentId, int? sectionId, int? termId,
        CancellationToken cancellationToken);
}
