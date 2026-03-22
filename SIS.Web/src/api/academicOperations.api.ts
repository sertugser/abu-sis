import type { AxiosError } from 'axios';
import apiClient from './axios';
import type {
  AcademicOperationsContext,
  AcademicPlanningSummary,
  DepartmentClassList,
  DepartmentCurriculumOverview,
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

export async function getDepartmentClassLists(departmentId: number, termId?: number): Promise<DepartmentClassList> {
  const { data } = await apiClient.get<DepartmentClassList>('/academicoperations/class-lists', {
    params: { departmentId, termId },
  });
  return data;
}

export async function getDepartmentCurriculumOverview(departmentId: number): Promise<DepartmentCurriculumOverview> {
  const { data } = await apiClient.get<DepartmentCurriculumOverview>('/academicoperations/curriculum-overview', {
    params: { departmentId },
  });
  return data;
}

export function getAcademicOperationsErrorMessage(error: unknown, fallback: string) {
  const axiosError = error as AxiosError<{ error?: string }>;
  const status = axiosError.response?.status;

  if (status === 403) {
    return 'Bu ekran icin yetkiniz bulunmuyor.';
  }

  if (status === 404) {
    return axiosError.response?.data?.error ?? 'Istenen akademik operasyon verisi bulunamadi.';
  }

  if (status === 400) {
    return axiosError.response?.data?.error ?? fallback;
  }

  return axiosError.response?.data?.error ?? fallback;
}
