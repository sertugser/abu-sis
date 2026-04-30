import { useQuery } from '@tanstack/react-query';
import { getActiveTerm } from '../api/course.api';
import { QUERY_KEYS } from '../lib/constants';

export function useActiveTerm() {
  return useQuery({
    queryKey: QUERY_KEYS.activeTerm,
    queryFn: getActiveTerm,
    staleTime: 0,
    refetchOnMount: 'always',
    refetchOnWindowFocus: 'always',
  });
}
