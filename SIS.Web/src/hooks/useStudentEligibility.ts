import { useQuery } from '@tanstack/react-query';
import { getStudentEligibility } from '../api/eligibility.api';
import { QUERY_KEYS } from '../lib/constants';

export function useStudentEligibility(studentId: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.studentEligibility(studentId ?? undefined),
    queryFn: () => getStudentEligibility(studentId!),
    enabled: studentId !== null && studentId > 0,
    staleTime: 60 * 1000,
  });
}
