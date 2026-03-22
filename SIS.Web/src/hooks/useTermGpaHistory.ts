import { useQuery } from '@tanstack/react-query';
import { getTermGpaHistory } from '../api/student.api';
import { QUERY_KEYS } from '../lib/constants';

export function useTermGpaHistory() {
  return useQuery({
    queryKey: QUERY_KEYS.termGpaHistory,
    queryFn: getTermGpaHistory,
    staleTime: 5 * 60 * 1000,
  });
}
