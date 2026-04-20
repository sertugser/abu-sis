import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  deleteAdvisorConstraint,
  getAdvisorStudents,
  getStudentAdvisorConstraints,
  updateAdvisorConstraint,
  upsertAdvisorConstraint,
} from '../api/advisorConstraint.api';
import { QUERY_KEYS } from '../lib/constants';
import type {
  UpdateAdvisorCourseConstraintRequest,
  UpsertAdvisorCourseConstraintRequest,
} from '../types/advisorConstraint';

export function useAdvisorStudents() {
  return useQuery({
    queryKey: QUERY_KEYS.advisorStudents,
    queryFn: getAdvisorStudents,
  });
}

export function useAdvisorConstraints(studentId: number | null, termId?: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.advisorConstraints(studentId ?? undefined, termId ?? undefined),
    queryFn: () => getStudentAdvisorConstraints(studentId!, termId),
    enabled: studentId !== null && studentId > 0,
  });
}

export function useUpsertAdvisorConstraint() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpsertAdvisorCourseConstraintRequest) => upsertAdvisorConstraint(request),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.advisorConstraints(data.studentId, data.termId) });
    },
  });
}

export function useUpdateAdvisorConstraint() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: number; request: UpdateAdvisorCourseConstraintRequest }) =>
      updateAdvisorConstraint(id, request),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.advisorConstraints(data.studentId, data.termId) });
    },
  });
}

export function useDeleteAdvisorConstraint() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id }: { id: number }) => deleteAdvisorConstraint(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['advisor', 'constraints'] });
    },
  });
}
