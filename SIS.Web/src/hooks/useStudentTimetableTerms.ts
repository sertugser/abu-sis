import { useQuery } from '@tanstack/react-query';
import { getStudentTimetableTerms } from '@/api/studentTimetable.api';

export function useStudentTimetableTerms() {
  return useQuery({
    queryKey: ['student-timetable-terms'],
    queryFn: getStudentTimetableTerms,
    staleTime: 10 * 60_000,
  });
}
