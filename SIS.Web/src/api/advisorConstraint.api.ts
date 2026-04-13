import apiClient from './axios';
import type {
  AdvisorCourseConstraintDto,
  AdvisorStudentListItemDto,
  UpdateAdvisorCourseConstraintRequest,
  UpsertAdvisorCourseConstraintRequest,
} from '../types/advisorConstraint';

export async function getAdvisorStudents(): Promise<AdvisorStudentListItemDto[]> {
  const response = await apiClient.get<AdvisorStudentListItemDto[]>('/advisor/students');
  return response.data;
}

export async function getStudentAdvisorConstraints(
  studentId: number,
  termId?: number | null
): Promise<AdvisorCourseConstraintDto[]> {
  const response = await apiClient.get<AdvisorCourseConstraintDto[]>(`/advisor/constraints/student/${studentId}`, {
    params: termId ? { termId } : undefined,
  });
  return response.data;
}

export async function upsertAdvisorConstraint(
  request: UpsertAdvisorCourseConstraintRequest
): Promise<AdvisorCourseConstraintDto> {
  const response = await apiClient.post<AdvisorCourseConstraintDto>('/advisor/constraints', request);
  return response.data;
}

export async function updateAdvisorConstraint(
  id: number,
  request: UpdateAdvisorCourseConstraintRequest
): Promise<AdvisorCourseConstraintDto> {
  const response = await apiClient.put<AdvisorCourseConstraintDto>(`/advisor/constraints/${id}`, request);
  return response.data;
}

export async function deleteAdvisorConstraint(id: number): Promise<void> {
  await apiClient.delete(`/advisor/constraints/${id}`);
}
