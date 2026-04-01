import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  applyInstructorSectionSchedule,
  getInstructorTimetable,
  suggestInstructorSectionSchedule,
  validateInstructorSectionSchedule,
} from '../api/instructorScheduling.api';
import type { ApplyScheduleRequest } from '../types/scheduling';

export function useInstructorTimetable(instructorId: number | undefined, termId?: number) {
  return useQuery({
    queryKey: ['instructorTimetable', instructorId, termId],
    queryFn: () => getInstructorTimetable(instructorId!, termId),
    enabled: !!instructorId,
    staleTime: 2 * 60 * 1000,
  });
}

export function useValidateInstructorSchedule() {
  return useMutation({
    mutationFn: ({ sectionId }: { sectionId: number }) => validateInstructorSectionSchedule(sectionId),
  });
}

export function useSuggestInstructorSchedule() {
  return useMutation({
    mutationFn: ({ sectionId }: { sectionId: number }) => suggestInstructorSectionSchedule(sectionId),
  });
}

export function useApplyInstructorSchedule() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      sectionId,
      request,
    }: {
      sectionId: number;
      request: ApplyScheduleRequest;
    }) => applyInstructorSectionSchedule(sectionId, request),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['instructorSections'] });
      await queryClient.invalidateQueries({ queryKey: ['instructorTimetable'] });
    },
  });
}
