import { useQuery } from '@tanstack/react-query';
import { getStudentInfo } from '../api/student.api';
import { QUERY_KEYS } from '../lib/constants';

export function useStudentInfo() {
  return useQuery({
    queryKey: QUERY_KEYS.studentInfo,
    queryFn: getStudentInfo,
    staleTime: 0,
    refetchOnMount: 'always',
  });
}
