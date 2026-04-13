import { useQuery } from '@tanstack/react-query';
import { getStudentGradeStatistics } from '../api/student.api';
import { QUERY_KEYS } from '../lib/constants';

export function useStudentGradeStatistics(registrationId: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.studentGradeStatistics(registrationId),
    queryFn: () => getStudentGradeStatistics(registrationId!),
    enabled: registrationId !== null,
    staleTime: 2 * 60 * 1000,
  });
}
