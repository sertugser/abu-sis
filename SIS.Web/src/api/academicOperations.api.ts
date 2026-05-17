import { isAxiosError } from 'axios';
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

type ApiErrorPayload = {
  error?: unknown;
  message?: unknown;
  detail?: unknown;
  title?: unknown;
  errors?: unknown;
};

export function getAcademicOperationsErrorMessage(error: unknown, fallback: string) {
  if (!isAxiosError(error)) {
    return error instanceof Error && error.message ? error.message : fallback;
  }

  const status = error.response?.status;
  const payload = normalizeErrorPayload(error.response?.data);

  if (!error.response) {
    return 'API sunucusuna ulaşılamadı. Backend çalışıyor mu kontrol edin.';
  }

  if (status === 401) {
    return 'Oturum bilgisi bulunamadı veya süresi doldu. Lütfen tekrar giriş yapın.';
  }

  if (status === 403) {
    return 'Bu ekran için yetkiniz bulunmuyor.';
  }

  if (status === 404) {
    return readErrorMessage(payload) ?? 'İstenen akademik operasyon verisi bulunamadı.';
  }

  if (status === 400) {
    return readErrorMessage(payload) ?? fallback;
  }

  return readErrorMessage(payload) ?? fallback;
}

function normalizeErrorPayload(data: unknown): ApiErrorPayload | null {
  if (typeof data === 'string') {
    const trimmed = data.trim();
    if (!trimmed) return null;

    try {
      const parsed = JSON.parse(trimmed) as unknown;
      return isRecord(parsed) ? parsed : { message: trimmed };
    } catch {
      return { message: trimmed };
    }
  }

  return isRecord(data) ? data : null;
}

function readErrorMessage(payload: ApiErrorPayload | null): string | null {
  if (!payload) return null;

  const directMessage = firstString(payload.error)
    ?? firstString(payload.message)
    ?? firstString(payload.detail);
  if (directMessage) return directMessage;

  const validationMessage = readValidationError(payload.errors);
  if (validationMessage) return validationMessage;

  return firstString(payload.title);
}

function readValidationError(errors: unknown): string | null {
  if (!errors) return null;

  if (Array.isArray(errors)) {
    return errors.map(firstString).find(Boolean) ?? null;
  }

  if (!isRecord(errors)) return firstString(errors);

  for (const value of Object.values(errors)) {
    const message = Array.isArray(value)
      ? value.map(firstString).find(Boolean)
      : firstString(value);
    if (message) return message;
  }

  return null;
}

function firstString(value: unknown): string | null {
  if (typeof value === 'string' && value.trim()) return value.trim();
  if (Array.isArray(value)) return value.map(firstString).find(Boolean) ?? null;
  return null;
}

function isRecord(value: unknown): value is ApiErrorPayload {
  return typeof value === 'object' && value !== null;
}
