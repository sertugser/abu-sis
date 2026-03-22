import { useQuery } from '@tanstack/react-query';
import {
  getAcademicOperationsContext,
  getAcademicPlanningSummary,
  getDepartmentClassLists,
  getDepartmentCurriculumOverview,
} from '../api/academicOperations.api';
import { QUERY_KEYS } from '../lib/constants';

export function useAcademicOperationsContext() {
  return useQuery({
    queryKey: QUERY_KEYS.academicOperationsContext,
    queryFn: getAcademicOperationsContext,
    staleTime: 5 * 60 * 1000,
  });
}

export function useAcademicPlanningSummary(departmentId: number | null, termId?: number, enabled = true) {
  return useQuery({
    queryKey: QUERY_KEYS.academicPlanningSummary(departmentId ?? undefined, termId),
    queryFn: () => getAcademicPlanningSummary(departmentId!, termId),
    enabled: enabled && departmentId !== null,
    staleTime: 60 * 1000,
  });
}

export function useDepartmentClassLists(departmentId: number | null, termId?: number, enabled = true) {
  return useQuery({
    queryKey: QUERY_KEYS.departmentClassLists(departmentId ?? undefined, termId),
    queryFn: () => getDepartmentClassLists(departmentId!, termId),
    enabled: enabled && departmentId !== null,
    staleTime: 60 * 1000,
  });
}

export function useDepartmentCurriculumOverview(departmentId: number | null, enabled = true) {
  return useQuery({
    queryKey: QUERY_KEYS.departmentCurriculumOverview(departmentId ?? undefined),
    queryFn: () => getDepartmentCurriculumOverview(departmentId!),
    enabled: enabled && departmentId !== null,
    staleTime: 5 * 60 * 1000,
  });
}
