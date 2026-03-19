import { useQuery } from '@tanstack/react-query';
import { getCurriculumStatus } from '../api/student.api';
import { QUERY_KEYS } from '../lib/constants';

export function useCurriculumStatus() {
  return useQuery({
    queryKey: QUERY_KEYS.curriculumStatus,
    queryFn: getCurriculumStatus,
    staleTime: 5 * 60 * 1000,
  });
}
