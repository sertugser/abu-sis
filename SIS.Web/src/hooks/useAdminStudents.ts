import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import apiClient from '../api/axios';
import type { PagedResult } from './useAdminUsers';

const API = '/admin/students';

export interface AdminStudentListItem {
  id: number;
  studentNumber: string;
  firstName: string;
  lastName: string;
  email: string;
  cgpa: number | null;
  completedSemesters: number | null;
  status: string;
  programName: string;
  programCode: string;
  departmentName: string;
  advisorName?: string;
  transcriptAccessible?: boolean;
  transcriptAccessMessage?: string;
}

export interface AdminStudentDetail extends AdminStudentListItem {
  userId: number;
  sgpa: number | null;
  frozenSemesterCount: number | null;
  isScholarshipStudent: boolean;
  hasFinancialBlock: boolean;
  advisorId?: number;
  programId: number | null;
  curriculumId: number | null;
  programType: string;
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
  advisorId?: number;
  hasAdvisor?: boolean;
  curriculumId?: number;
  classYear?: number;
  studentNoFrom?: string;
  studentNoTo?: string;
  enrollmentFrom?: string;
  enrollmentTo?: string;
  hasFailingCourse?: boolean;
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

export function useAdminStudents(filter: AdminStudentFilter) {
  return useQuery({
    queryKey: ['admin', 'students', 'list', filter],
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
    queryKey: ['admin', 'students', 'detail', id],
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

// Reserved for the upcoming advisor panel (kart "Danışman Atama"); not yet wired.
export function useAssignAdvisor() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: async ({ studentId, advisorId }: { studentId: number; advisorId: number }) => {
      await apiClient.put(`${API}/${studentId}/advisor`, { advisorId });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'students'] }),
  });
}

export interface UpdateStudentAcademic {
  status: string;
  advisorId?: number;
  programId: number;
  curriculumId: number;
  programType: string;
  isScholarshipStudent: boolean;
  hasFinancialBlock: boolean;
}

export function useUpdateStudentAcademic() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: async ({ studentId, dto }: { studentId: number; dto: UpdateStudentAcademic }) => {
      await apiClient.put(`${API}/${studentId}/academic`, dto);
    },
    onSuccess: (_d, { studentId }) => {
      qc.invalidateQueries({ queryKey: ['admin', 'students', 'detail', studentId] });
      qc.invalidateQueries({ queryKey: ['admin', 'students', 'list'] });
    },
  });
}
