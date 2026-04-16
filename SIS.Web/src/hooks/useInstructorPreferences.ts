import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getInstructorPreferences,
  createInstructorPreference,
  updateInstructorPreference,
  deleteInstructorPreference,
  approveInstructorPreference,
  getPreferencesSummary,
} from '../api/instructorPreference.api';
import type { CreateInstructorPreferenceRequest } from '../types/instructorPreference';

const PREF_KEY = 'instructorPreferences';

export function useInstructorPreferences(instructorId: number | undefined, termId?: number) {
  return useQuery({
    queryKey: [PREF_KEY, instructorId, termId],
    queryFn: () => getInstructorPreferences(instructorId!, termId),
    enabled: !!instructorId,
  });
}

export function usePreferencesSummary(termId?: number, departmentId?: number) {
  return useQuery({
    queryKey: [PREF_KEY, 'summary', termId, departmentId],
    queryFn: () => getPreferencesSummary(termId, departmentId),
  });
}

export function useCreatePreference() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ instructorId, request }: { instructorId: number; request: CreateInstructorPreferenceRequest }) =>
      createInstructorPreference(instructorId, request),
    onSuccess: () => qc.invalidateQueries({ queryKey: [PREF_KEY] }),
  });
}

export function useUpdatePreference() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ prefId, request }: { prefId: number; request: CreateInstructorPreferenceRequest }) =>
      updateInstructorPreference(prefId, request),
    onSuccess: () => qc.invalidateQueries({ queryKey: [PREF_KEY] }),
  });
}

export function useDeletePreference() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (prefId: number) => deleteInstructorPreference(prefId),
    onSuccess: () => qc.invalidateQueries({ queryKey: [PREF_KEY] }),
  });
}

export function useApprovePreference() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ prefId, approved }: { prefId: number; approved: boolean }) =>
      approveInstructorPreference(prefId, approved),
    onSuccess: () => qc.invalidateQueries({ queryKey: [PREF_KEY] }),
  });
}
