import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { curriculumApi } from '../api/curriculum.api';
import type {
  CreateCurriculumRequest,
  UpdateCurriculumRequest,
  AddCourseRequest,
  UpdateCourseRequest,
  CopyCurriculumRequest,
  CreatePoolRequest,
  UpdatePoolRequest,
} from '../types/curriculumManagement';

export interface CurriculumListFilters {
  programId?: number;
  departmentId?: number;
  facultyId?: number;
}

const KEYS = {
  list: (filters?: CurriculumListFilters) =>
    ['curriculum', 'list', filters?.programId, filters?.departmentId, filters?.facultyId] as const,
  detail: (id: number) => ['curriculum', 'detail', id] as const,
  courses: (id: number) => ['curriculum', 'courses', id] as const,
  pools: (id: number) => ['curriculum', 'pools', id] as const,
};

export function useCurriculumList(filters?: CurriculumListFilters) {
  const hasFilter = !!(filters?.programId || filters?.departmentId || filters?.facultyId);
  return useQuery({
    queryKey: KEYS.list(filters),
    queryFn: () => curriculumApi.list(filters),
    enabled: hasFilter,
    staleTime: 2 * 60 * 1000,
  });
}

export function useCurriculumDetail(id: number | null) {
  return useQuery({
    queryKey: KEYS.detail(id!),
    queryFn: () => curriculumApi.get(id!),
    enabled: id !== null,
    staleTime: 60 * 1000,
  });
}

function invalidateAll(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: ['curriculum'] });
}

export function useCreateCurriculum() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateCurriculumRequest) => curriculumApi.create(req),
    onSuccess: () => invalidateAll(qc),
  });
}

export function useUpdateCurriculum() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...req }: { id: number } & UpdateCurriculumRequest) => curriculumApi.update(id, req),
    onSuccess: () => invalidateAll(qc),
  });
}

export function useDeleteCurriculum() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => curriculumApi.delete(id),
    onSuccess: () => invalidateAll(qc),
  });
}

export function useAddCurriculumCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ curriculumId, ...req }: { curriculumId: number } & AddCourseRequest) =>
      curriculumApi.addCourse(curriculumId, req),
    onSuccess: () => invalidateAll(qc),
  });
}

export function useUpdateCurriculumCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ curriculumId, courseId, ...req }: { curriculumId: number; courseId: number } & UpdateCourseRequest) =>
      curriculumApi.updateCourse(curriculumId, courseId, req),
    onSuccess: () => invalidateAll(qc),
  });
}

export function useRemoveCurriculumCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ curriculumId, courseId }: { curriculumId: number; courseId: number }) =>
      curriculumApi.removeCourse(curriculumId, courseId),
    onSuccess: () => invalidateAll(qc),
  });
}

export function useActivateCurriculum() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => curriculumApi.activate(id),
    onSuccess: () => invalidateAll(qc),
  });
}

export function useCopyCurriculum() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ sourceId, ...req }: { sourceId: number } & CopyCurriculumRequest) =>
      curriculumApi.copy(sourceId, req),
    onSuccess: () => invalidateAll(qc),
  });
}

export function useCurriculumPools(curriculumId: number | null) {
  return useQuery({
    queryKey: KEYS.pools(curriculumId!),
    queryFn: () => curriculumApi.listPools(curriculumId!),
    enabled: curriculumId !== null,
    staleTime: 60 * 1000,
  });
}

export function useCreatePool() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ curriculumId, ...req }: { curriculumId: number } & CreatePoolRequest) =>
      curriculumApi.createPool(curriculumId, req),
    onSuccess: () => invalidateAll(qc),
  });
}

export function useUpdatePool() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ curriculumId, poolId, ...req }: { curriculumId: number; poolId: number } & UpdatePoolRequest) =>
      curriculumApi.updatePool(curriculumId, poolId, req),
    onSuccess: () => invalidateAll(qc),
  });
}

export function useDeletePool() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ curriculumId, poolId }: { curriculumId: number; poolId: number }) =>
      curriculumApi.deletePool(curriculumId, poolId),
    onSuccess: () => invalidateAll(qc),
  });
}
