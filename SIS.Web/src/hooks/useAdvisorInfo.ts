import { useQuery } from '@tanstack/react-query';
import { getAdvisorSchedule, getMyAdvisor } from '@/api/advisorInfo.api';

export const useAdvisorInfo = () =>
  useQuery({
    queryKey: ['advisor-info', 'me'],
    queryFn: getMyAdvisor,
    staleTime: 5 * 60_000,
  });

/**
 * Lazy fetch — sadece Sheet açıldığında (enabled=true) çağrılır.
 */
export const useAdvisorSchedule = (enabled: boolean) =>
  useQuery({
    queryKey: ['advisor-info', 'schedule'],
    queryFn: getAdvisorSchedule,
    enabled,
    staleTime: 60_000,
    retry: false,
  });
