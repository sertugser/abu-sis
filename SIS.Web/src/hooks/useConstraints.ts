import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getConstraints,
  getConstraintDetail,
  toggleConstraint,
  updateConstraintCategory,
  updateConstraintParameters,
  addConstraintException,
  deleteConstraintException,
  getScoreBreakdown,
  updateConstraintWeight,
} from '../api/constraint.api';
import { QUERY_KEYS } from '../lib/constants';
import type { AddExceptionRequest } from '../types/constraint';

export function useConstraints() {
  return useQuery({
    queryKey: QUERY_KEYS.constraints,
    queryFn: getConstraints,
    staleTime: 60 * 1000,
  });
}

export function useConstraintDetail(id: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.constraintDetail(id!),
    queryFn: () => getConstraintDetail(id!),
    enabled: id !== null,
    staleTime: 30 * 1000,
  });
}

export function useToggleConstraint() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => toggleConstraint(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.constraints });
    },
  });
}

export function useUpdateConstraintCategory() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, category }: { id: number; category: 'Hard' | 'Soft' }) =>
      updateConstraintCategory(id, category),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.constraints });
    },
  });
}

export function useUpdateConstraintParameters() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, parameters }: { id: number; parameters: string }) =>
      updateConstraintParameters(id, parameters),
    onSuccess: (_data, variables) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.constraints });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.constraintDetail(variables.id) });
    },
  });
}

export function useAddConstraintException() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ constraintId, request }: { constraintId: number; request: AddExceptionRequest }) =>
      addConstraintException(constraintId, request),
    onSuccess: (_data, variables) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.constraints });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.constraintDetail(variables.constraintId) });
    },
  });
}

export function useDeleteConstraintException() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (exceptionId: number) => deleteConstraintException(exceptionId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.constraints });
      // Invalidate all constraint details
      qc.invalidateQueries({ queryKey: ['constraints', 'detail'] });
    },
  });
}

export function useScoreBreakdown(termId: number | null) {
  return useQuery({
    queryKey: [...QUERY_KEYS.constraints, 'score-breakdown', termId],
    queryFn: () => getScoreBreakdown(termId!),
    enabled: termId != null,
    staleTime: 30 * 1000,
  });
}

export function useUpdateConstraintWeight() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, weight }: { id: number; weight: number }) => updateConstraintWeight(id, weight),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.constraints });
    },
  });
}
