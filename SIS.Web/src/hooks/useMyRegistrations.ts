import { useQuery } from '@tanstack/react-query';
import { getMyRegistrations } from '../api/registration.api';
import { QUERY_KEYS } from '../lib/constants';

export function useMyRegistrations() {
  return useQuery({
    queryKey: QUERY_KEYS.myRegistrations,
    queryFn: getMyRegistrations,
    staleTime: 30 * 1000,
  });
}
