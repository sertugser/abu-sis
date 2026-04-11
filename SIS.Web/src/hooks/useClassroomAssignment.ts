import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getClassroomAssignments,
  createClassroomAssignment,
  deleteClassroomAssignment,
  bulkAssignClassrooms,
  suggestClassroomAssignments,
  type CreateClassroomTermAssignmentDto,
  type BulkClassroomTermAssignmentDto,
} from '../api/classroomAssignment.api';

const ASSIGNMENTS_KEY = ['admin', 'classroomAssignments'] as const;

export function useClassroomAssignments(termId: number | null) {
  return useQuery({
    queryKey: [...ASSIGNMENTS_KEY, termId],
    queryFn: () => getClassroomAssignments(termId!),
    enabled: termId !== null && termId > 0,
    staleTime: 30 * 1000,
  });
}

export function useCreateClassroomAssignment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: CreateClassroomTermAssignmentDto) => createClassroomAssignment(dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ASSIGNMENTS_KEY });
    },
  });
}

export function useDeleteClassroomAssignment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => deleteClassroomAssignment(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ASSIGNMENTS_KEY });
    },
  });
}

export function useBulkAssignClassrooms() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: BulkClassroomTermAssignmentDto) => bulkAssignClassrooms(dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ASSIGNMENTS_KEY });
    },
  });
}

export function useSuggestClassroomAssignments() {
  return useMutation({
    mutationFn: (termId: number) => suggestClassroomAssignments(termId),
  });
}
