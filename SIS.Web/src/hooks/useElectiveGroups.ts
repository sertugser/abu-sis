import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { electiveGroupApi } from '../api/electiveGroup.api';
import type {
  CreateElectiveGroupRequest,
  UpdateElectiveGroupRequest,
  AddCoursesToGroupRequest,
} from '../types/electiveGroup';

const KEYS = {
  list: (subtypeId?: number, facultyId?: number, departmentId?: number, isActive?: boolean) =>
    ['electiveGroups', 'list', subtypeId, facultyId, departmentId, isActive] as const,
  detail: (id: number) => ['electiveGroups', 'detail', id] as const,
  courses: (id: number) => ['electiveGroups', 'courses', id] as const,
};

function invalidateAll(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: ['electiveGroups'] });
}

/**
 * Faz B B3c-3: subtypeId (FK) DB-driven filter. Eski enum filter kaldırıldı —
 * artık custom subtype'lar da filter'a düşer.
 */
export function useElectiveGroupList(subtypeId?: number, facultyId?: number, departmentId?: number, isActive?: boolean) {
  return useQuery({
    queryKey: KEYS.list(subtypeId, facultyId, departmentId, isActive),
    queryFn: () => electiveGroupApi.list(subtypeId, facultyId, departmentId, isActive),
    staleTime: 2 * 60 * 1000,
  });
}

export function useElectiveGroupDetail(id: number | null) {
  return useQuery({
    queryKey: KEYS.detail(id!),
    queryFn: () => electiveGroupApi.get(id!),
    enabled: id !== null,
    staleTime: 60 * 1000,
  });
}

export function useCreateElectiveGroup() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateElectiveGroupRequest) => electiveGroupApi.create(req),
    onSuccess: () => invalidateAll(qc),
  });
}

export function useUpdateElectiveGroup() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...req }: { id: number } & UpdateElectiveGroupRequest) =>
      electiveGroupApi.update(id, req),
    onSuccess: () => invalidateAll(qc),
  });
}

export function useDeleteElectiveGroup() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => electiveGroupApi.delete(id),
    onSuccess: () => invalidateAll(qc),
  });
}

export function useAddCoursesToGroup() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ groupId, ...req }: { groupId: number } & AddCoursesToGroupRequest) =>
      electiveGroupApi.addCourses(groupId, req),
    onSuccess: () => invalidateAll(qc),
  });
}

export function useRemoveCourseFromGroup() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ groupId, courseId }: { groupId: number; courseId: number }) =>
      electiveGroupApi.removeCourse(groupId, courseId),
    onSuccess: () => invalidateAll(qc),
  });
}
