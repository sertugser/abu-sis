import { useQuery } from '@tanstack/react-query';
import { getStudentGradeStatistics } from '../api/student.api';
import { QUERY_KEYS } from '../lib/constants';

export function useStudentGradeStatistics(registrationId: number | null, commonEvaluation = false) {
  return useQuery({
    queryKey: QUERY_KEYS.studentGradeStatistics(registrationId, commonEvaluation),
    queryFn: () => getStudentGradeStatistics(registrationId!, commonEvaluation),
    enabled: registrationId !== null,
    staleTime: 2 * 60 * 1000,
  });
}
