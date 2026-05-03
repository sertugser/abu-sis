import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { QUERY_KEYS } from '../lib/constants';
import apiClient from '../api/axios';
import type { PagedResult } from './useAdminUsers';

const API = '/admin/students';

export interface AdminStudentListItem {
  id: number;
  studentNumber: string;
  firstName: string;
  lastName: string;
  email: string;
  cgpa: number;
  completedSemesters: number;
  status: string;
  programName: string;
  programCode: string;
  departmentName: string;
  advisorName?: string;
}

export interface AdminStudentDetail extends AdminStudentListItem {
  sgpa: number;
  frozenSemesterCount: number;
  isScholarshipStudent: boolean;
  hasFinancialBlock: boolean;
  advisorId?: number;
}

export interface AdminStudentFilter {
  search?: string;
  programId?: number;
  departmentId?: number;
  status?: string;
  minCgpa?: number;
  maxCgpa?: number;
  page?: number;
  pageSize?: number;
}

export interface CreateStudentDto {
  firstName: string;
  lastName: string;
  nationalId: string;
  programId: number;
  entryYear: number;
  isScholarshipStudent: boolean;
  password: string;
  advisorId?: number;
  overrideStudentNumber?: string;
  overrideEmail?: string;
}

export interface BulkAdvisorAssignmentPreviewRequest {
  departmentId: number;
  programId?: number | null;
  includeStudentsWithAdvisor: boolean;
  page?: number;
  pageSize?: number;
}

export interface BulkAdvisorAssignmentApplyRequest {
  assignments: BulkAdvisorAssignmentApplyItem[];
}

export interface BulkAdvisorAssignmentApplyItem {
  studentId: number;
  advisorId: number;
}

export interface BulkAdvisorCandidate {
  advisorId: number;
  advisorName: string;
  departmentId: number;
  departmentName: string;
  currentStudentCount: number;
  plannedNewStudentCount: number;
  plannedTotalStudentCount: number;
  isAvailableForAdvisorAssignment: boolean;
  advisorAssignmentCapacity?: number | null;
}

export interface BulkAdvisorAssignmentPreviewItem {
  studentId: number;
  studentNumber: string;
  studentName: string;
  programId: number;
  programName: string;
  departmentId: number;
  departmentName: string;
  completedSemesters: number;
  currentAdvisorId?: number | null;
  currentAdvisorName?: string | null;
  suggestedAdvisorId: number;
  suggestedAdvisorName: string;
  reason: string;
}

export interface BulkAdvisorAssignmentPreviewResponse {
  departmentId: number;
  departmentName: string;
  programId?: number | null;
  programName?: string | null;
  totalStudentCount: number;
  plannedAssignmentCount: number;
  alreadyAssignedCount: number;
  page: number;
  pageSize: number;
  totalAssignmentCount: number;
  totalPages: number;
  advisors: BulkAdvisorCandidate[];
  assignments: BulkAdvisorAssignmentPreviewItem[];
  warnings: string[];
}

export interface BulkAdvisorAssignmentApplyResponse {
  updatedCount: number;
  skippedCount: number;
  skippedItems: BulkAdvisorAssignmentSkippedItem[];
}

export interface BulkAdvisorAssignmentSkippedItem {
  studentId: number;
  reason: string;
}

export interface BulkAdvisorSettingsUpdateRequest {
  advisors: BulkAdvisorSettingUpdateItem[];
}

export interface BulkAdvisorSettingUpdateItem {
  advisorId: number;
  isAvailableForAdvisorAssignment: boolean;
  advisorAssignmentCapacity?: number | null;
}

export interface BulkAdvisorSettingsUpdateResponse {
  updatedCount: number;
}

export function useAdminStudents(filter: AdminStudentFilter) {
  return useQuery({
    queryKey: QUERY_KEYS.adminStudents(
      filter.search,
      filter.programId,
      filter.departmentId,
      filter.status,
      filter.page
    ),
    queryFn: async () => {
      const { data } = await apiClient.get<PagedResult<AdminStudentListItem>>(API, {
        params: filter,
      });

      return data;
    },
  });
}

export function useAdminStudent(id: number | null) {
  return useQuery({
    queryKey: ['admin', 'students', id],
    queryFn: async () => {
      const { data } = await apiClient.get<AdminStudentDetail>(`${API}/${id}`);
      return data;
    },
    enabled: id !== null,
  });
}

export function useCreateStudent() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: async (dto: CreateStudentDto) => {
      const { data } = await apiClient.post<AdminStudentDetail>(API, dto);
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'students'] }),
  });
}

export function useAssignAdvisor() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: async ({ studentId, advisorId }: { studentId: number; advisorId: number }) => {
      await apiClient.put(`${API}/${studentId}/advisor`, { advisorId });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'students'] }),
  });
}

export function usePreviewBulkAdvisorAssignment() {
  return useMutation({
    mutationFn: async (dto: BulkAdvisorAssignmentPreviewRequest) => {
      const { data } = await apiClient.post<BulkAdvisorAssignmentPreviewResponse>(
        `${API}/bulk-advisor-assignment/preview`,
        dto
      );

      return data;
    },
  });
}

export function useApplyBulkAdvisorAssignment() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: async (dto: BulkAdvisorAssignmentApplyRequest) => {
      const { data } = await apiClient.post<BulkAdvisorAssignmentApplyResponse>(
        `${API}/bulk-advisor-assignment/apply`,
        dto
      );

      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['admin', 'students'] });
    },
  });
}

export function useUpdateBulkAdvisorSettings() {
  return useMutation({
    mutationFn: async (dto: BulkAdvisorSettingsUpdateRequest) => {
      const { data } = await apiClient.put<BulkAdvisorSettingsUpdateResponse>(
        `${API}/bulk-advisor-assignment/advisor-settings`,
        dto
      );

      return data;
    },
  });
}