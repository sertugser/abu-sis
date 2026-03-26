import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getTermClosingStatus,
  lockGrades,
  unlockGrades,
  calculateGpa,
  previewPromotion,
  promote,
  rollbackPromotion,
  openNewTerm,
} from '../api/termClosing.api';
import type { NewTermRequestDto } from '../api/termClosing.api';

const TERM_CLOSING_KEY = (departmentId: number) => ['termClosing', departmentId] as const;
const PROMOTION_PREVIEW_KEY = (departmentId: number) => ['termClosing', 'preview', departmentId] as const;

export function useTermClosingStatus(departmentId: number | null) {
  return useQuery({
    queryKey: TERM_CLOSING_KEY(departmentId ?? 0),
    queryFn: () => getTermClosingStatus(departmentId!),
    enabled: departmentId !== null && departmentId > 0,
    staleTime: 10 * 1000,
  });
}

export function usePromotionPreview(departmentId: number | null, enabled: boolean) {
  return useQuery({
    queryKey: PROMOTION_PREVIEW_KEY(departmentId ?? 0),
    queryFn: () => previewPromotion(departmentId!),
    enabled: enabled && departmentId !== null && departmentId > 0,
    staleTime: 30 * 1000,
  });
}

export function useLockGrades(departmentId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => lockGrades(departmentId),
    onSuccess: () => qc.invalidateQueries({ queryKey: TERM_CLOSING_KEY(departmentId) }),
  });
}

export function useUnlockGrades(departmentId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => unlockGrades(departmentId),
    onSuccess: () => qc.invalidateQueries({ queryKey: TERM_CLOSING_KEY(departmentId) }),
  });
}

export function useCalculateGpa(departmentId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => calculateGpa(departmentId),
    onSuccess: () => qc.invalidateQueries({ queryKey: TERM_CLOSING_KEY(departmentId) }),
  });
}

export function usePromote(departmentId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => promote(departmentId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: TERM_CLOSING_KEY(departmentId) });
      qc.invalidateQueries({ queryKey: PROMOTION_PREVIEW_KEY(departmentId) });
    },
  });
}

export function useRollbackPromotion(departmentId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => rollbackPromotion(departmentId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: TERM_CLOSING_KEY(departmentId) });
      qc.invalidateQueries({ queryKey: PROMOTION_PREVIEW_KEY(departmentId) });
    },
  });
}

export function useOpenNewTerm(departmentId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (request: NewTermRequestDto) => openNewTerm(departmentId, request),
    onSuccess: () => qc.invalidateQueries({ queryKey: TERM_CLOSING_KEY(departmentId) }),
  });
}
