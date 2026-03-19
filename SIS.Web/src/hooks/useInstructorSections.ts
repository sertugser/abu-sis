import { useQuery } from '@tanstack/react-query';
import { getInstructorSections, getSectionStudents } from '../api/instructor.api';

export function useInstructorSections() {
  return useQuery({
    queryKey: ['instructorSections'],
    queryFn: getInstructorSections,
    staleTime: 2 * 60 * 1000,
  });
}

export function useSectionStudents(sectionId: number | null) {
  return useQuery({
    queryKey: ['sectionStudents', sectionId],
    queryFn: () => getSectionStudents(sectionId!),
    enabled: sectionId !== null,
    staleTime: 60 * 1000,
  });
}
