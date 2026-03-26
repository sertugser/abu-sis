import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getOpenedCourses,
  getCurriculumCoursesForOpening,
  getDepartmentInstructors,
  openCourse,
  bulkOpenCourses,
  updateOpenedSection,
  closeSection,
} from '../api/courseOpening.api';
import { QUERY_KEYS } from '../lib/constants';
import type { OpenCourseRequest, BulkOpenCoursesRequest, UpdateOpenedSectionRequest } from '../types/courseOpening';

export function useOpenedCourses(departmentId: number | null, termId: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.courseOpeningList(departmentId ?? 0, termId ?? 0),
    queryFn: () => getOpenedCourses(departmentId!, termId!),
    enabled: departmentId !== null && termId !== null,
    staleTime: 30 * 1000,
  });
}

export function useCurriculumCoursesForOpening(departmentId: number | null, termId: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.curriculumCoursesForOpening(departmentId ?? 0, termId ?? 0),
    queryFn: () => getCurriculumCoursesForOpening(departmentId!, termId!),
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

export function useOpenCourse(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: OpenCourseRequest) => openCourse(departmentId, termId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.courseOpeningList(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.curriculumCoursesForOpening(departmentId, termId) });
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
    },
  });
}

export function useUpdateOpenedSection(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateOpenedSectionRequest) => updateOpenedSection(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.courseOpeningList(departmentId, termId) });
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
    },
  });
}
