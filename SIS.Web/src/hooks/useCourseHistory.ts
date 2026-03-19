import { useQuery } from '@tanstack/react-query';
import { getCourseHistory } from '../api/registration.api';

export function useCourseHistory() {
  return useQuery({
    queryKey: ['courseHistory'],
    queryFn: getCourseHistory,
    staleTime: 5 * 60 * 1000,
  });
}
