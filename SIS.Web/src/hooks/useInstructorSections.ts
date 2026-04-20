import { useQuery } from '@tanstack/react-query';
import { getInstructorSections, getInstructorTerms, getSectionStudents } from '../api/instructor.api';

export function useInstructorSections(termId?: number | null) {
  return useQuery({
    queryKey: ['instructorSections', termId ?? 'active'],
    queryFn: () => getInstructorSections(termId ?? undefined),
    staleTime: 2 * 60 * 1000,
  });
}

export function useInstructorTerms() {
  return useQuery({
    queryKey: ['instructorTerms'],
    queryFn: getInstructorTerms,
    staleTime: 5 * 60 * 1000,
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
