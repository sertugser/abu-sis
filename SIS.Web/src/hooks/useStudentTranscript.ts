import { useQuery } from '@tanstack/react-query';
import { getStudentTranscript } from '../api/student.api';
import { QUERY_KEYS } from '../lib/constants';

interface UseStudentTranscriptOptions {
  forDocument?: boolean;
}

export function useStudentTranscript(options?: UseStudentTranscriptOptions) {
  return useQuery({
    queryKey: [...QUERY_KEYS.studentTranscript, options?.forDocument ? 'document' : 'default'],
    queryFn: () => getStudentTranscript(options),
  });
}
