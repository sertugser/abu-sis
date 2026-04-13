import { useQuery } from '@tanstack/react-query';
import { getMyRecommendations } from '../api/recommendation.api';
import { QUERY_KEYS } from '../lib/constants';

export function useRecommendations(pacePreference = 50) {
  return useQuery({
    queryKey: QUERY_KEYS.recommendations(pacePreference),
    queryFn: () => getMyRecommendations(pacePreference),
    staleTime: 60 * 1000,
  });
}
