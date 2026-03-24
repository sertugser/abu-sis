import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { QUERY_KEYS } from '@/lib/constants';
import {
  applyInstructorSectionSchedule,
  createInstructorAvailability,
  deleteInstructorAvailability,
  getInstructorAvailabilities,
  suggestInstructorSectionSchedule,
  updateInstructorAvailability,
  validateInstructorSectionSchedule,
} from '../api/instructorScheduling.api';
import type { ApplyScheduleRequest, UpsertInstructorAvailabilityRequest } from '../types/scheduling';

export function useInstructorAvailabilities() {
  return useQuery({
    queryKey: QUERY_KEYS.instructorAvailabilities,
    queryFn: getInstructorAvailabilities,
    staleTime: 60 * 1000,
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
    },
  });
}

export function useCreateInstructorAvailability() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: UpsertInstructorAvailabilityRequest) => createInstructorAvailability(payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: QUERY_KEYS.instructorAvailabilities });
    },
  });
}

export function useUpdateInstructorAvailability() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      availabilityId,
      payload,
    }: {
      availabilityId: number;
      payload: UpsertInstructorAvailabilityRequest;
    }) => updateInstructorAvailability(availabilityId, payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: QUERY_KEYS.instructorAvailabilities });
    },
  });
}

export function useDeleteInstructorAvailability() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (availabilityId: number) => deleteInstructorAvailability(availabilityId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: QUERY_KEYS.instructorAvailabilities });
    },
  });
}
