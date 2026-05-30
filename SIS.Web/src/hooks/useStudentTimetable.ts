import { useQuery } from '@tanstack/react-query';
import { getStudentTimetable } from '@/api/studentTimetable.api';

export function useStudentTimetable(termId: number | undefined) {
  return useQuery({
    queryKey: ['student-timetable', termId],
    queryFn: () => getStudentTimetable(termId),
    enabled: termId !== undefined,
    staleTime: 5 * 60_000,
  });
}
