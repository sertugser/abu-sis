import { useQuery } from '@tanstack/react-query';
import { getStudentCatalog } from '../api/course.api';
import { QUERY_KEYS } from '../lib/constants';

export function useStudentCatalog() {
  return useQuery({
    queryKey: QUERY_KEYS.studentCatalog,
    queryFn: getStudentCatalog,
    staleTime: 2 * 60 * 1000,
  });
}
