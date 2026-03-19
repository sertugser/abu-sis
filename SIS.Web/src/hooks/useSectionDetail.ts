import { useQuery } from '@tanstack/react-query';
import { getSectionDetail } from '../api/course.api';

export function useSectionDetail(sectionId: number | null) {
  return useQuery({
    queryKey: ['sectionDetail', sectionId],
    queryFn: () => getSectionDetail(sectionId!),
    enabled: sectionId !== null,
    staleTime: 2 * 60 * 1000,
  });
}
