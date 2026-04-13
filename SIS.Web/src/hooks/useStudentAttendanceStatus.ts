import { useQuery } from '@tanstack/react-query';
import { getMyAttendanceStatus } from '../api/registration.api';
import { QUERY_KEYS } from '../lib/constants';

export function useStudentAttendanceStatus(termId?: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.studentAttendanceStatus(termId),
    queryFn: () => getMyAttendanceStatus(termId),
    staleTime: 30 * 1000,
  });
}
