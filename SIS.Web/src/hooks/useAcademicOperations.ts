import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  bulkAssignAdvisors,
  bulkAssignCurriculum,
  getAcademicOperationsContext,
  getAcademicPlanningSummary,
  getDepartmentAcademicLevels,
  getDepartmentClassLists,
  getDepartmentCurriculumOptions,
  getDepartmentCurriculumOverview,
  getDepartmentStudents,
  getGradeLockReadiness,
  type DepartmentStudentQuery,
} from '../api/academicOperations.api';
import { QUERY_KEYS } from '../lib/constants';
import type { BulkAdvisorAssignmentRequest, BulkCurriculumAssignmentRequest } from '../types/academicOperations';

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

export function useDepartmentStudents(
  departmentId: number | null,
  query: DepartmentStudentQuery = {},
  enabled = true,
) {
  return useQuery({
    queryKey: [
      'academicOperations',
      'departmentStudents',
      departmentId,
      query.search ?? '',
      query.status ?? '',
      query.programId ?? null,
      query.curriculumId ?? null,
      query.advisorId ?? null,
    ],
    queryFn: () => getDepartmentStudents(departmentId!, query),
    enabled: enabled && departmentId !== null,
    staleTime: 60 * 1000,
  });
}

export function useDepartmentCurriculumOptions(departmentId: number | null, enabled = true) {
  return useQuery({
    queryKey: ['academicOperations', 'curriculumOptions', departmentId],
    queryFn: () => getDepartmentCurriculumOptions(departmentId!),
    enabled: enabled && departmentId !== null,
    staleTime: 5 * 60 * 1000,
  });
}

export function useBulkAdvisorAssignment(departmentId: number | null) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: BulkAdvisorAssignmentRequest) => bulkAssignAdvisors(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['academicOperations', 'departmentStudents', departmentId] });
      queryClient.invalidateQueries({ queryKey: ['departmentClassLists', departmentId] });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.departmentInstructors(departmentId ?? 0) });
    },
  });
}

export function useBulkCurriculumAssignment(departmentId: number | null) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: BulkCurriculumAssignmentRequest) => bulkAssignCurriculum(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['academicOperations', 'departmentStudents', departmentId] });
      queryClient.invalidateQueries({ queryKey: ['academicOperations', 'curriculumOptions', departmentId] });
      if (departmentId !== null) {
        queryClient.invalidateQueries({ queryKey: QUERY_KEYS.forecastDepartmentSummary(departmentId) });
        queryClient.invalidateQueries({ queryKey: QUERY_KEYS.academicPlanningSummary(departmentId) });
      }
    },
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
