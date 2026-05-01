import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import apiClient from '../api/axios';

const BASE = '/admin/academic-structure';

export interface FacultyDto { id: number; name: string; code: string; departmentCount: number; }
export interface DepartmentDto {
  id: number;
  name: string;
  code: string;
  facultyId: number;
  facultyName: string;
  programCount: number;
  studentCount: number;
  instructorCount: number;
  isServiceUnit: boolean;
}
export interface ProgramDto {
  id: number; name: string; code: string;
  externalSystemCode: string | null;
  departmentId: number; departmentName: string;
  degreeLevel: string; educationType: string;
  maxStudyDurationSemesters: number; totalRequiredEcts: number;
  curriculumCount: number;
}

export interface ProgramUpsertDto {
  name: string;
  code: string;
  externalSystemCode: string | null;
  departmentId: number;
  degreeLevel: string;
  educationType: string;
  maxStudyDurationSemesters: number;
  totalRequiredEcts: number;
}

export function useAdminFaculties() {
  return useQuery({
    queryKey: ['admin', 'structure', 'faculties'],
    queryFn: async () => { const { data } = await apiClient.get<FacultyDto[]>(`${BASE}/faculties`); return data; },
    staleTime: 2 * 60 * 1000,
  });
}

export function useAdminDepartmentsByStructure(facultyId?: number) {
  return useQuery({
    queryKey: ['admin', 'structure', 'departments', facultyId],
    queryFn: async () => {
      const { data } = await apiClient.get<DepartmentDto[]>(`${BASE}/departments`, { params: facultyId ? { facultyId } : undefined });
      return data;
    },
    staleTime: 2 * 60 * 1000,
  });
}

export function useAdminProgramsByStructure(departmentId?: number) {
  return useQuery({
    queryKey: ['admin', 'structure', 'programs', departmentId],
    queryFn: async () => {
      const { data } = await apiClient.get<ProgramDto[]>(`${BASE}/programs`, { params: departmentId ? { departmentId } : undefined });
      return data;
    },
    staleTime: 2 * 60 * 1000,
  });
}

function invalidateStructure(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: ['admin', 'structure'] });
  qc.invalidateQueries({ queryKey: ['admin', 'lookup'] });
}

export function useCreateFaculty() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dto: { name: string; code: string }) => {
      const { data } = await apiClient.post<FacultyDto>(`${BASE}/faculties`, dto);
      return data;
    },
    onSuccess: () => invalidateStructure(qc),
  });
}

export function useUpdateFaculty() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...dto }: { id: number; name: string; code: string }) => {
      const { data } = await apiClient.put<FacultyDto>(`${BASE}/faculties/${id}`, dto);
      return data;
    },
    onSuccess: () => invalidateStructure(qc),
  });
}

export function useDeleteFaculty() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => { await apiClient.delete(`${BASE}/faculties/${id}`); },
    onSuccess: () => invalidateStructure(qc),
  });
}

export function useCreateDepartment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dto: { name: string; code: string; facultyId: number }) => {
      const { data } = await apiClient.post<DepartmentDto>(`${BASE}/departments`, dto);
      return data;
    },
    onSuccess: () => invalidateStructure(qc),
  });
}

export function useUpdateDepartment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...dto }: { id: number; name: string; code: string; facultyId: number }) => {
      const { data } = await apiClient.put<DepartmentDto>(`${BASE}/departments/${id}`, dto);
      return data;
    },
    onSuccess: () => invalidateStructure(qc),
  });
}

export function useDeleteDepartment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => { await apiClient.delete(`${BASE}/departments/${id}`); },
    onSuccess: () => invalidateStructure(qc),
  });
}

export function useCreateProgram() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dto: ProgramUpsertDto) => {
      const { data } = await apiClient.post<ProgramDto>(`${BASE}/programs`, dto);
      return data;
    },
    onSuccess: () => invalidateStructure(qc),
  });
}

export function useUpdateProgram() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...dto }: { id: number } & ProgramUpsertDto) => {
      const { data } = await apiClient.put<ProgramDto>(`${BASE}/programs/${id}`, dto);
      return data;
    },
    onSuccess: () => invalidateStructure(qc),
  });
}

export function useDeleteProgram() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => { await apiClient.delete(`${BASE}/programs/${id}`); },
    onSuccess: () => invalidateStructure(qc),
  });
}
