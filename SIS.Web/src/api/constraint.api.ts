import apiClient from './axios';
import type {
  ConstraintDefinitionDto,
  ConstraintDefinitionDetailDto,
  ConstraintExceptionDto,
  AddExceptionRequest,
  ExceptionRequestDto,
  CreateExceptionRequestDto,
  ApprovalActionDto,
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

export async function updateConstraintWeight(id: number, weight: number): Promise<void> {
  await apiClient.put(`/constraints/${id}/weight`, { weight });
}

// ── Exception Requests (Approval Workflow) ──────────────────────
export async function createExceptionRequest(dto: CreateExceptionRequestDto): Promise<{ id: number }> {
  const { data } = await apiClient.post<{ id: number }>('/constraints/exception-requests', dto);
  return data;
}

export async function getPendingExceptionRequests(): Promise<ExceptionRequestDto[]> {
  const { data } = await apiClient.get<ExceptionRequestDto[]>('/constraints/exception-requests/pending');
  return data;
}

export async function getMyExceptionRequests(): Promise<ExceptionRequestDto[]> {
  const { data } = await apiClient.get<ExceptionRequestDto[]>('/constraints/exception-requests/my');
  return data;
}

export async function getExceptionRequest(id: number): Promise<ExceptionRequestDto> {
  const { data } = await apiClient.get<ExceptionRequestDto>(`/constraints/exception-requests/${id}`);
  return data;
}

export async function approveExceptionRequest(id: number, dto: ApprovalActionDto): Promise<void> {
  await apiClient.post(`/constraints/exception-requests/${id}/approve`, dto);
}

export async function rejectExceptionRequest(id: number, dto: ApprovalActionDto): Promise<void> {
  await apiClient.post(`/constraints/exception-requests/${id}/reject`, dto);
}
