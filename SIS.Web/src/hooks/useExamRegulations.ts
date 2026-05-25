import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createExamRegulation,
  deleteExamRegulation,
  getExamRegulations,
  updateExamRegulation,
  cloneExamRegulation,
  getAttendancePolicy,
} from '../api/examRegulation.api';
import type { SaveExamRegulationRequest } from '../types/examRegulation';

const ROOT = ['examRegulations'] as const;

export function useExamRegulations(programId?: number) {
  return useQuery({
    queryKey: [...ROOT, 'list', programId ?? null],
    queryFn: () => getExamRegulations(programId),
    staleTime: 5 * 60 * 1000,
  });
}

export function useCreateExamRegulation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: SaveExamRegulationRequest) => createExamRegulation(dto),
    onSuccess: () => qc.invalidateQueries({ queryKey: ROOT }),
  });
}

export function useUpdateExamRegulation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: number; dto: SaveExamRegulationRequest }) => updateExamRegulation(id, dto),
    onSuccess: () => qc.invalidateQueries({ queryKey: ROOT }),
  });
}

export function useDeleteExamRegulation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => deleteExamRegulation(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ROOT }),
  });
}

export function useCloneExamRegulation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: { programId?: number; year: number } }) =>
      cloneExamRegulation(id, payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: ROOT }),
  });
}

export function useAttendancePolicy() {
  return useQuery({
    queryKey: [...ROOT, 'attendance-policy'],
    queryFn: getAttendancePolicy,
    staleTime: 5 * 60 * 1000,
  });
}
