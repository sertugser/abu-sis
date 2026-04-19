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
  createExceptionRequest,
  getPendingExceptionRequests,
  getMyExceptionRequests,
  approveExceptionRequest,
  rejectExceptionRequest,
} from '../api/constraint.api';
import { QUERY_KEYS } from '../lib/constants';
import type { AddExceptionRequest, CreateExceptionRequestDto, ApprovalActionDto } from '../types/constraint';

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

// ── Exception Requests (Approval Workflow) ──────────────────────
export function usePendingExceptionRequests() {
  return useQuery({
    queryKey: QUERY_KEYS.pendingExceptionRequests,
    queryFn: getPendingExceptionRequests,
    staleTime: 30 * 1000,
  });
}

export function useMyExceptionRequests() {
  return useQuery({
    queryKey: QUERY_KEYS.myExceptionRequests,
    queryFn: getMyExceptionRequests,
    staleTime: 30 * 1000,
  });
}

export function useCreateExceptionRequest() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: CreateExceptionRequestDto) => createExceptionRequest(dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.exceptionRequests });
    },
  });
}

export function useApproveExceptionRequest() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: number; dto: ApprovalActionDto }) => approveExceptionRequest(id, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.exceptionRequests });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.constraints });
    },
  });
}

export function useRejectExceptionRequest() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: number; dto: ApprovalActionDto }) => rejectExceptionRequest(id, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.exceptionRequests });
    },
  });
}
