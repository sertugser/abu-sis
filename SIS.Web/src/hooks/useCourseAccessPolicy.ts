import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import apiClient from '../api/axios';

const BASE = '/course-access-policy';

export interface AccessPolicyDto {
  id: number;
  courseId: number;
  courseCode: string;
  termId: number;
  facultyId: number | null;
  facultyName: string | null;
  departmentId: number | null;
  departmentName: string | null;
}

interface CreateAccessPolicyDto {
  courseId: number;
  termId: number;
  facultyId?: number | null;
  departmentId?: number | null;
}

export function useAccessPolicies(courseId: number | null, termId: number | null) {
  return useQuery({
    queryKey: ['accessPolicies', courseId, termId],
    queryFn: async () => {
      const { data } = await apiClient.get<AccessPolicyDto[]>(BASE, {
        params: { courseId, termId },
      });
      return data;
    },
    enabled: courseId !== null && termId !== null,
    staleTime: 60 * 1000,
  });
}

export function useCreateAccessPolicy() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dto: CreateAccessPolicyDto) => {
      const { data } = await apiClient.post<AccessPolicyDto>(BASE, dto);
      return data;
    },
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: ['accessPolicies', vars.courseId, vars.termId] });
    },
  });
}

export function useDeleteAccessPolicy() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, courseId, termId }: { id: number; courseId: number; termId: number }) => {
      await apiClient.delete(`${BASE}/${id}`);
      return { courseId, termId };
    },
    onSuccess: (result) => {
      qc.invalidateQueries({ queryKey: ['accessPolicies', result.courseId, result.termId] });
    },
  });
}
