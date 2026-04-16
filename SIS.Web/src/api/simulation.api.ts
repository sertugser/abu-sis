import apiClient from './axios';
import type {
  SimulateMoveRequest,
  SimulateConstraintChangeRequest,
  SimulationResult,
  SuggestionItem,
} from '../types/simulation';

export async function simulateMove(request: SimulateMoveRequest): Promise<SimulationResult> {
  const { data } = await apiClient.post<SimulationResult>('/constraints/simulate/move', request);
  return data;
}

export async function getSuggestions(
  sectionId: number,
  termId: number,
  limit = 10,
): Promise<SuggestionItem[]> {
  const { data } = await apiClient.get<SuggestionItem[]>(
    `/constraints/simulate/suggestions?sectionId=${sectionId}&termId=${termId}&limit=${limit}`,
  );
  return data;
}

export async function simulateConstraintChange(
  request: SimulateConstraintChangeRequest,
): Promise<SimulationResult> {
  const { data } = await apiClient.post<SimulationResult>(
    '/constraints/simulate/constraint-change',
    request,
  );
  return data;
}
