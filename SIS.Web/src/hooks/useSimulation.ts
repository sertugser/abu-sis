import { useMutation, useQuery } from '@tanstack/react-query';
import { simulateMove, getSuggestions, simulateConstraintChange } from '../api/simulation.api';
import type { SimulateMoveRequest, SimulateConstraintChangeRequest } from '../types/simulation';

export function useSimulateMove() {
  return useMutation({
    mutationFn: (request: SimulateMoveRequest) => simulateMove(request),
  });
}

export function useSuggestions(sectionId: number | null, termId: number | null, limit = 10) {
  return useQuery({
    queryKey: ['simulation', 'suggestions', sectionId, termId, limit],
    queryFn: () => getSuggestions(sectionId!, termId!, limit),
    enabled: sectionId !== null && termId !== null,
    staleTime: 30 * 1000,
  });
}

export function useSimulateConstraintChange() {
  return useMutation({
    mutationFn: (request: SimulateConstraintChangeRequest) => simulateConstraintChange(request),
  });
}
