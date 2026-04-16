import apiClient from './axios';
import type {
  ConstraintProfileDto,
  ConstraintProfileDetailDto,
  ApplyProfileResult,
} from '../types/constraintProfile';

export async function getConstraintProfiles(): Promise<ConstraintProfileDto[]> {
  const { data } = await apiClient.get<ConstraintProfileDto[]>('/constraints/profiles');
  return data;
}

export async function getConstraintProfileDetail(id: number): Promise<ConstraintProfileDetailDto> {
  const { data } = await apiClient.get<ConstraintProfileDetailDto>(`/constraints/profiles/${id}`);
  return data;
}

export async function createProfileFromCurrent(
  name: string,
  description?: string,
): Promise<{ id: number; name: string }> {
  const { data } = await apiClient.post<{ id: number; name: string }>(
    '/constraints/profiles/from-current',
    { name, description },
  );
  return data;
}

export async function applyProfile(id: number): Promise<ApplyProfileResult> {
  const { data } = await apiClient.post<ApplyProfileResult>(`/constraints/profiles/${id}/apply`);
  return data;
}

export async function deleteProfile(id: number): Promise<void> {
  await apiClient.delete(`/constraints/profiles/${id}`);
}
