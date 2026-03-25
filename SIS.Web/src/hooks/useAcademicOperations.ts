import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  approveCourseSections,
  bulkApproveCourseSections,
  getAcademicOperationsContext,
  getAcademicPlanningSummary,
  getDepartmentAcademicLevels,
  getDepartmentClassLists,
  getDepartmentCurriculumOverview,
  getGradeLockReadiness,
  getSectionApprovals,
  rejectCourseSections,
} from '../api/academicOperations.api';
import { QUERY_KEYS } from '../lib/constants';
import type { ApproveCourseSectionsRequest, BulkApprovalRequest, RejectCourseSectionsRequest } from '../types/academicOperations';

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
    queryKey: ['gradeLockReadiness', departmentId, termId],
    queryFn: () => getGradeLockReadiness(departmentId!, termId),
    enabled: enabled && departmentId !== null,
    staleTime: 60 * 1000,
  });
}

// Section Approval hooks
export function useSectionApprovals(departmentId: number | null, termId: number | null, enabled = true) {
  return useQuery({
    queryKey: QUERY_KEYS.sectionApprovals(departmentId ?? undefined, termId ?? undefined),
    queryFn: () => getSectionApprovals(departmentId!, termId!),
    enabled: enabled && departmentId !== null && termId !== null,
    staleTime: 30 * 1000,
  });
}

export function useApproveCourseSections(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ApproveCourseSectionsRequest) => approveCourseSections(departmentId, termId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.sectionApprovals(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.academicPlanningSummary(departmentId, termId) });
    },
  });
}

export function useRejectCourseSections(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RejectCourseSectionsRequest) => rejectCourseSections(departmentId, termId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.sectionApprovals(departmentId, termId) });
    },
  });
}

export function useBulkApproveCourseSections(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: BulkApprovalRequest) => bulkApproveCourseSections(departmentId, termId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.sectionApprovals(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.academicPlanningSummary(departmentId, termId) });
    },
  });
}
