import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import apiClient from '../api/axios';

const BASE = '/admin/academic-structure';

export interface FacultyDto {
  id: number; name: string; code: string;
  departmentCount: number; programCount: number; studentCount: number;
  createdAt: string; updatedAt: string | null;
}
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
  createdAt: string;
  updatedAt: string | null;
}
export interface ProgramDto {
  id: number; name: string; code: string;
  externalSystemCode: string | null;
  departmentId: number; departmentName: string;
  degreeLevel: string; educationType: string;
  maxStudyDurationSemesters: number; totalRequiredEcts: number;
  curriculumCount: number;
  createdAt: string; updatedAt: string | null;
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

export interface FacultyDeletionSummary {
  facultyId: number;
  facultyName: string;
  departmentCount: number;
  programCount: number;
  curriculumCount: number;
  studentCount: number;
  instructorCount: number;
  canDelete: boolean;
}

export function useFacultyDeletionSummary(facultyId: number | null) {
  return useQuery({
    queryKey: ['admin', 'structure', 'faculty-deletion-summary', facultyId],
    queryFn: async () => {
      const { data } = await apiClient.get<FacultyDeletionSummary>(`${BASE}/faculties/${facultyId}/deletion-summary`);
      return data;
    },
    enabled: facultyId !== null,
    staleTime: 0,
  });
}

export interface FacultyDepartmentStat {
  id: number;
  name: string;
  code: string;
  isServiceUnit: boolean;
  studentCount: number;
  instructorCount: number;
  programCount: number;
}
export interface DegreeLevelCount { degreeLevel: string; count: number; }
export interface FacultyOverview {
  id: number;
  name: string;
  code: string;
  createdAt: string;
  updatedAt: string | null;
  departmentCount: number;
  programCount: number;
  curriculumCount: number;
  studentCount: number;
  instructorCount: number;
  programsWithoutCurriculum: number;
  programsWithoutExternalCode: number;
  departments: FacultyDepartmentStat[];
  programsByDegree: DegreeLevelCount[];
}

export function useFacultyOverview(facultyId: number | null) {
  return useQuery({
    queryKey: ['admin', 'structure', 'faculty-overview', facultyId],
    queryFn: async () => {
      const { data } = await apiClient.get<FacultyOverview>(`${BASE}/faculties/${facultyId}/overview`);
      return data;
    },
    enabled: facultyId !== null,
    staleTime: 60 * 1000,
  });
}

export interface DepartmentProgramStat {
  id: number;
  name: string;
  code: string;
  degreeLevel: string;
  educationType: string;
  totalRequiredEcts: number;
  maxStudyDurationSemesters: number;
  curriculumCount: number;
  hasExternalCode: boolean;
  studentCount: number;
}
export interface DepartmentInstructor { id: number; name: string; title: string; }
export interface DepartmentOverview {
  id: number;
  name: string;
  code: string;
  facultyId: number;
  facultyName: string;
  isServiceUnit: boolean;
  createdAt: string;
  updatedAt: string | null;
  studentCount: number;
  instructorCount: number;
  programCount: number;
  curriculumCount: number;
  programsWithoutCurriculum: number;
  programsWithoutExternalCode: number;
  programs: DepartmentProgramStat[];
  instructors: DepartmentInstructor[];
  programsByDegree: DegreeLevelCount[];
}

export function useDepartmentOverview(departmentId: number | null) {
  return useQuery({
    queryKey: ['admin', 'structure', 'department-overview', departmentId],
    queryFn: async () => {
      const { data } = await apiClient.get<DepartmentOverview>(`${BASE}/departments/${departmentId}/overview`);
      return data;
    },
    enabled: departmentId !== null,
    staleTime: 60 * 1000,
  });
}

export interface DepartmentDeletionSummary {
  departmentId: number;
  departmentName: string;
  programCount: number;
  curriculumCount: number;
  studentCount: number;
  instructorCount: number;
  canDelete: boolean;
}

export function useDepartmentDeletionSummary(departmentId: number | null) {
  return useQuery({
    queryKey: ['admin', 'structure', 'department-deletion-summary', departmentId],
    queryFn: async () => {
      const { data } = await apiClient.get<DepartmentDeletionSummary>(`${BASE}/departments/${departmentId}/deletion-summary`);
      return data;
    },
    enabled: departmentId !== null,
    staleTime: 0,
  });
}

export interface ProgramDeletionSummary {
  programId: number;
  programName: string;
  curriculumCount: number;
  studentCount: number;
  canDelete: boolean;
}

export function useProgramDeletionSummary(programId: number | null) {
  return useQuery({
    queryKey: ['admin', 'structure', 'program-deletion-summary', programId],
    queryFn: async () => {
      const { data } = await apiClient.get<ProgramDeletionSummary>(`${BASE}/programs/${programId}/deletion-summary`);
      return data;
    },
    enabled: programId !== null,
    staleTime: 0,
  });
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

export interface BulkRowResult { index: number; label: string; status: string; message: string | null; }
export interface BulkImportResult { createdCount: number; failedCount: number; rows: BulkRowResult[]; }

export function useBulkCreateFaculties() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (items: { name: string; code: string }[]) => {
      const { data } = await apiClient.post<BulkImportResult>(`${BASE}/faculties/bulk`, items);
      return data;
    },
    onSuccess: () => invalidateStructure(qc),
  });
}

export function useBulkCreateDepartments() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (items: { name: string; code: string; facultyId: number }[]) => {
      const { data } = await apiClient.post<BulkImportResult>(`${BASE}/departments/bulk`, items);
      return data;
    },
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
