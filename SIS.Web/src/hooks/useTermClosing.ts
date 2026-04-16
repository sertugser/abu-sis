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
  getGradeReadinessDetail,
  getGradeDistribution,
  getDepartmentClosingOverview,
  exportPromotionReport,
} from '../api/termClosing.api';
import type { NewTermRequestDto } from '../api/termClosing.api';

const TERM_CLOSING_KEY = (departmentId: number) => ['termClosing', departmentId] as const;
const PROMOTION_PREVIEW_KEY = (departmentId: number) => ['termClosing', 'preview', departmentId] as const;
const GRADE_DETAIL_KEY = (departmentId: number) => ['termClosing', 'gradeDetail', departmentId] as const;
const GRADE_DIST_KEY = (departmentId: number) => ['termClosing', 'gradeDist', departmentId] as const;
const DEPT_OVERVIEW_KEY = (facultyId?: number) => ['termClosing', 'overview', facultyId] as const;

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

// ── New hooks ──

export function useGradeReadinessDetail(departmentId: number | null) {
  return useQuery({
    queryKey: GRADE_DETAIL_KEY(departmentId ?? 0),
    queryFn: () => getGradeReadinessDetail(departmentId!),
    enabled: departmentId !== null && departmentId > 0,
    staleTime: 30 * 1000,
  });
}

export function useGradeDistribution(departmentId: number | null, enabled: boolean) {
  return useQuery({
    queryKey: GRADE_DIST_KEY(departmentId ?? 0),
    queryFn: () => getGradeDistribution(departmentId!),
    enabled: enabled && departmentId !== null && departmentId > 0,
    staleTime: 60 * 1000,
  });
}

export function useDepartmentClosingOverview(facultyId?: number) {
  return useQuery({
    queryKey: DEPT_OVERVIEW_KEY(facultyId),
    queryFn: () => getDepartmentClosingOverview(facultyId),
    staleTime: 30 * 1000,
  });
}

export function useExportPromotionReport(departmentId: number) {
  return useMutation({
    mutationFn: () => exportPromotionReport(departmentId),
    onSuccess: (blob) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'sinif-atlatma-raporu.xlsx';
      a.click();
      URL.revokeObjectURL(url);
    },
  });
}
