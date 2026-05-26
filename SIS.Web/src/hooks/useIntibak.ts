import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { intibakApi, type IntibakBulkItem } from '../api/intibak.api';

export function useIntibakCurricula() {
  return useQuery({
    queryKey: ['intibak', 'curricula'],
    queryFn: () => intibakApi.listCurricula(),
    staleTime: 60 * 1000,
  });
}

export function useIntibakCourses(curriculumId: number | null) {
  return useQuery({
    queryKey: ['intibak', 'courses', curriculumId],
    queryFn: () => intibakApi.getCourses(curriculumId as number),
    enabled: curriculumId !== null,
    staleTime: 30 * 1000,
  });
}

export function useCourseIntibakStatistics(courseId: number | null) {
  return useQuery({
    queryKey: ['intibak', 'course-statistics', courseId],
    queryFn: () => intibakApi.getCourseStatistics(courseId as number),
    enabled: courseId !== null,
    staleTime: 60 * 1000,
  });
}

export function useSaveIntibak(curriculumId: number | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (items: IntibakBulkItem[]) => intibakApi.save(curriculumId as number, items),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['intibak', 'courses', curriculumId] });
      qc.invalidateQueries({ queryKey: ['intibak', 'curricula'] });
    },
  });
}
