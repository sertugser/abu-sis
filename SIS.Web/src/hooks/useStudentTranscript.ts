import { useQuery } from '@tanstack/react-query';
import { getStudentTranscript } from '../api/student.api';
import { QUERY_KEYS } from '../lib/constants';

export function useStudentTranscript() {
  return useQuery({
    queryKey: QUERY_KEYS.studentTranscript,
    queryFn: getStudentTranscript,
    staleTime: 5 * 60 * 1000,
  });
}
