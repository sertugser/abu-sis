import apiClient from './axios';
import type { InstructorPreferenceDto, CreateInstructorPreferenceRequest } from '../types/instructorPreference';

export async function getInstructorPreferences(
  instructorId: number,
  termId?: number,
): Promise<InstructorPreferenceDto[]> {
  const { data } = await apiClient.get(`/instructors/${instructorId}/preferences`, {
    params: termId ? { termId } : undefined,
  });
  return data;
}

export async function createInstructorPreference(
  instructorId: number,
  request: CreateInstructorPreferenceRequest,
): Promise<InstructorPreferenceDto> {
  const { data } = await apiClient.post(`/instructors/${instructorId}/preferences`, request);
  return data;
}

export async function updateInstructorPreference(
  prefId: number,
  request: CreateInstructorPreferenceRequest,
): Promise<InstructorPreferenceDto> {
  const { data } = await apiClient.put(`/instructors/preferences/${prefId}`, request);
  return data;
}

export async function deleteInstructorPreference(prefId: number): Promise<void> {
  await apiClient.delete(`/instructors/preferences/${prefId}`);
}

export async function approveInstructorPreference(
  prefId: number,
  approved: boolean = true,
): Promise<InstructorPreferenceDto> {
  const { data } = await apiClient.put(`/instructors/preferences/${prefId}/approve`, null, {
    params: { approved },
  });
  return data;
}

export async function getPreferencesSummary(
  termId?: number,
  departmentId?: number,
): Promise<InstructorPreferenceDto[]> {
  const { data } = await apiClient.get('/instructors/preferences/summary', {
    params: { termId, departmentId },
  });
  return data;
}
