import apiClient from './axios';
import type {
  ConstraintDefinitionDto,
  ConstraintDefinitionDetailDto,
  ConstraintExceptionDto,
  AddExceptionRequest,
} from '../types/constraint';

export async function getConstraints(): Promise<ConstraintDefinitionDto[]> {
  const { data } = await apiClient.get<ConstraintDefinitionDto[]>('/constraints');
  return data;
}

export async function getConstraintDetail(id: number): Promise<ConstraintDefinitionDetailDto> {
  const { data } = await apiClient.get<ConstraintDefinitionDetailDto>(`/constraints/${id}`);
  return data;
}

export async function toggleConstraint(id: number): Promise<void> {
  await apiClient.put(`/constraints/${id}/toggle`);
}

export async function updateConstraintCategory(id: number, category: 'Hard' | 'Soft'): Promise<void> {
  await apiClient.put(`/constraints/${id}/category`, { category });
}

export async function updateConstraintParameters(id: number, parameters: string): Promise<void> {
  await apiClient.put(`/constraints/${id}/parameters`, { parameters });
}

export async function addConstraintException(
  constraintId: number,
  request: AddExceptionRequest,
): Promise<ConstraintExceptionDto> {
  const { data } = await apiClient.post<ConstraintExceptionDto>(
    `/constraints/${constraintId}/exceptions`,
    request,
  );
  return data;
}

export async function deleteConstraintException(exceptionId: number): Promise<void> {
  await apiClient.delete(`/constraints/exceptions/${exceptionId}`);
}
