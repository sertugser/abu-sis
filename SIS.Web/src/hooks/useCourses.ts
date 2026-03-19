import { useQuery } from '@tanstack/react-query';
import { getCourses } from '../api/course.api';
import { QUERY_KEYS } from '../lib/constants';

export function useCourses(programId?: number) {
  return useQuery({
    queryKey: QUERY_KEYS.courses(programId),
    queryFn: () => getCourses(programId),
    staleTime: 2 * 60 * 1000,
  });
}
