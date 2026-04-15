import { useQuery } from '@tanstack/react-query';
import {
  getAcademicOperationsContext,
  getAcademicPlanningSummary,
  getDepartmentAcademicLevels,
  getDepartmentClassLists,
  getDepartmentCurriculumOverview,
  getGradeLockReadiness,
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

export function useDepartmentClassLists(departmentId: number | null, termId?: number, enabled = true, search?: string) {
  return useQuery({
    queryKey: [...QUERY_KEYS.departmentClassLists(departmentId ?? undefined, termId), search ?? ''],
    queryFn: () => getDepartmentClassLists(departmentId!, termId, search),
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

export function useDepartmentAcademicLevels(departmentId: number | null, enabled = true) {
  return useQuery({
    queryKey: QUERY_KEYS.departmentAcademicLevels(departmentId ?? undefined),
    queryFn: () => getDepartmentAcademicLevels(departmentId!),
    enabled: enabled && departmentId !== null,
    staleTime: 5 * 60 * 1000,
  });
}

export function useGradeLockReadiness(departmentId: number | null, termId?: number, enabled = true) {
  return useQuery({
    queryKey: QUERY_KEYS.gradeLockReadiness(departmentId ?? undefined, termId),
    queryFn: () => getGradeLockReadiness(departmentId!, termId),
    enabled: enabled && departmentId !== null,
    staleTime: 60 * 1000,
  });
}
