import type { AxiosError } from 'axios';
import apiClient from './axios';
import type {
  AcademicOperationsContext,
  AcademicPlanningSummary,
  BulkAcademicOperationResult,
  BulkAdvisorAssignmentRequest,
  BulkCurriculumAssignmentRequest,
  BulkAcademicLevelDto,
  DepartmentClassList,
  DepartmentCurriculumOption,
  DepartmentCurriculumOverview,
  DepartmentStudentList,
} from '../types/academicOperations';

export async function getAcademicOperationsContext(): Promise<AcademicOperationsContext> {
  const { data } = await apiClient.get<AcademicOperationsContext>('/academicoperations/context');
  return data;
}

export async function getAcademicPlanningSummary(departmentId: number, termId?: number): Promise<AcademicPlanningSummary> {
  const { data } = await apiClient.get<AcademicPlanningSummary>('/academicoperations/planning-summary', {
    params: { departmentId, termId },
  });
  return data;
}

export async function getDepartmentClassLists(departmentId: number, termId?: number, search?: string): Promise<DepartmentClassList> {
  const { data } = await apiClient.get<DepartmentClassList>('/academicoperations/class-lists', {
    params: { departmentId, termId, search: search || undefined },
  });
  return data;
}

export async function getDepartmentCurriculumOverview(departmentId: number): Promise<DepartmentCurriculumOverview> {
  const { data } = await apiClient.get<DepartmentCurriculumOverview>('/academicoperations/curriculum-overview', {
    params: { departmentId },
  });
  return data;
}

export interface DepartmentStudentQuery {
  search?: string;
  status?: string;
  programId?: number | null;
  curriculumId?: number | null;
  advisorId?: number | null;
}

export async function getDepartmentStudents(
  departmentId: number,
  query: DepartmentStudentQuery = {},
): Promise<DepartmentStudentList> {
  const { data } = await apiClient.get<DepartmentStudentList>('/academicoperations/students', {
    params: {
      departmentId,
      search: query.search || undefined,
      status: query.status || undefined,
      programId: query.programId || undefined,
      curriculumId: query.curriculumId || undefined,
      advisorId: query.advisorId || undefined,
    },
  });
  return data;
}

export async function getDepartmentCurriculumOptions(departmentId: number): Promise<DepartmentCurriculumOption[]> {
  const { data } = await apiClient.get<DepartmentCurriculumOption[]>('/academicoperations/curriculum-options', {
    params: { departmentId },
  });
  return data;
}

export async function bulkAssignAdvisors(request: BulkAdvisorAssignmentRequest): Promise<BulkAcademicOperationResult> {
  const { data } = await apiClient.post<BulkAcademicOperationResult>('/academicoperations/bulk-advisor-assignment', request);
  return data;
}

export async function bulkAssignCurriculum(request: BulkCurriculumAssignmentRequest): Promise<BulkAcademicOperationResult> {
  const { data } = await apiClient.post<BulkAcademicOperationResult>('/academicoperations/bulk-curriculum-assignment', request);
  return data;
}

export async function getDepartmentAcademicLevels(departmentId: number): Promise<BulkAcademicLevelDto> {
  const { data } = await apiClient.get<BulkAcademicLevelDto>(`/academicLevel/department/${departmentId}`);
  return data;
}

export interface GradeLockReadiness {
  totalSections: number;
  totalActiveRegistrations: number;
  missingGradeCount: number;
  allGradesEntered: boolean;
  hasActiveRegistrations: boolean;
  notes: string[];
}

export async function getGradeLockReadiness(departmentId: number, termId?: number): Promise<GradeLockReadiness> {
  const { data } = await apiClient.get<GradeLockReadiness>(`/academicprogression/grade-lock-readiness/${departmentId}`, {
    params: { termId },
  });
  return data;
}

export function getAcademicOperationsErrorMessage(error: unknown, fallback: string) {
  const axiosError = error as AxiosError<{ error?: string }>;
  const status = axiosError.response?.status;

  if (!axiosError.response) {
    return 'API sunucusuna ulaşılamadı. Backend çalışıyor mu kontrol edin.';
  }

  if (status === 401) {
    return 'Oturum bilgisi bulunamadı veya süresi doldu. Lütfen tekrar giriş yapın.';
  }

  if (status === 403) {
    return 'Bu ekran için yetkiniz bulunmuyor.';
  }

  if (status === 404) {
    return axiosError.response?.data?.error ?? 'İstenen akademik operasyon verisi bulunamadı.';
  }

  if (status === 400) {
    return axiosError.response?.data?.error ?? fallback;
  }

  return axiosError.response?.data?.error ?? fallback;
}
