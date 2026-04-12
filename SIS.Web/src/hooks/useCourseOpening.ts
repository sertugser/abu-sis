import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getOpenedCourses,
  getCurriculumCoursesForOpening,
  getDepartmentInstructors,
  getInstructorWorkload,
  openCourse,
  bulkOpenCourses,
  updateOpenedSection,
  closeSection,
  bulkAssignInstructor,
  getPreviousTermCourses,
  transferFromTerm,
} from '../api/courseOpening.api';
import { QUERY_KEYS } from '../lib/constants';
import type { OpenCourseRequest, BulkOpenCoursesRequest, UpdateOpenedSectionRequest, BulkAssignInstructorRequest, TransferFromTermRequest } from '../types/courseOpening';

export function useOpenedCourses(departmentId: number | null, termId: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.courseOpeningList(departmentId ?? 0, termId ?? 0),
    queryFn: () => getOpenedCourses(departmentId!, termId!),
    enabled: departmentId !== null && termId !== null,
    staleTime: 30 * 1000,
  });
}

export function useCurriculumCoursesForOpening(departmentId: number | null, termId: number | null, showAll = false) {
  return useQuery({
    queryKey: [...QUERY_KEYS.curriculumCoursesForOpening(departmentId ?? 0, termId ?? 0), showAll],
    queryFn: () => getCurriculumCoursesForOpening(departmentId!, termId!, showAll),
    enabled: departmentId !== null && termId !== null,
    staleTime: 60 * 1000,
  });
}

export function useDepartmentInstructors(departmentId: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.departmentInstructors(departmentId ?? 0),
    queryFn: () => getDepartmentInstructors(departmentId!),
    enabled: departmentId !== null,
    staleTime: 5 * 60 * 1000,
  });
}

export function useInstructorWorkload(instructorId: number | null, termId: number | null) {
  return useQuery({
    queryKey: ['courseOpening', 'workload', instructorId, termId],
    queryFn: () => getInstructorWorkload(instructorId!, termId!),
    enabled: instructorId !== null && instructorId > 0 && termId !== null,
    staleTime: 30 * 1000,
  });
}

export function useOpenCourse(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: OpenCourseRequest) => openCourse(departmentId, termId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.courseOpeningList(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.curriculumCoursesForOpening(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.forecastDepartmentSummary(departmentId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.academicPlanningSummary(departmentId, termId) });
    },
  });
}

export function useBulkOpenCourses(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: BulkOpenCoursesRequest) => bulkOpenCourses(departmentId, termId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.courseOpeningList(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.curriculumCoursesForOpening(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.forecastDepartmentSummary(departmentId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.academicPlanningSummary(departmentId, termId) });
    },
  });
}

export function useUpdateOpenedSection(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateOpenedSectionRequest) => updateOpenedSection(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.courseOpeningList(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.forecastDepartmentSummary(departmentId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.academicPlanningSummary(departmentId, termId) });
    },
  });
}

export function useBulkAssignInstructor(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: BulkAssignInstructorRequest) => bulkAssignInstructor(departmentId, termId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.courseOpeningList(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.academicPlanningSummary(departmentId, termId) });
    },
  });
}

export function usePreviousTermCourses(
  departmentId: number | null,
  sourceTermId: number | null,
  targetTermId: number | null,
) {
  return useQuery({
    queryKey: ['courseOpening', 'previousTerm', departmentId, sourceTermId, targetTermId],
    queryFn: () => getPreviousTermCourses(departmentId!, sourceTermId!, targetTermId!),
    enabled: departmentId !== null && sourceTermId !== null && targetTermId !== null,
    staleTime: 60 * 1000,
  });
}

export function useTransferFromTerm(departmentId: number, targetTermId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: TransferFromTermRequest) => transferFromTerm(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.courseOpeningList(departmentId, targetTermId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.curriculumCoursesForOpening(departmentId, targetTermId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.forecastDepartmentSummary(departmentId) });
    },
  });
}

export function useCloseSection(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (sectionId: number) => closeSection(sectionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.courseOpeningList(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.curriculumCoursesForOpening(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.forecastDepartmentSummary(departmentId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.academicPlanningSummary(departmentId, termId) });
    },
  });
}
