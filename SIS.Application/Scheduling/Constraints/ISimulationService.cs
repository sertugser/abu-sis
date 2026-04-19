using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.Scheduling.Constraints;

public interface ISimulationService
{
    Task<SimulationResult> SimulateMoveAsync(SimulateMoveRequest request, CancellationToken ct);
    Task<IReadOnlyList<SuggestionItem>> GetSuggestionsAsync(int sectionId, int termId, int limit, CancellationToken ct);
    Task<SimulationResult> SimulateConstraintChangeAsync(SimulateConstraintChangeRequest request, CancellationToken ct);
}
