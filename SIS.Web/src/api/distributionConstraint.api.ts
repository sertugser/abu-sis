import apiClient from './axios';
import type {
  DistributionConstraintDto,
  CreateDistributionConstraintRequest,
} from '../types/distributionConstraint';

export async function getDistributionConstraints(
  termId: number,
): Promise<DistributionConstraintDto[]> {
  const { data } = await apiClient.get<DistributionConstraintDto[]>(
    '/constraints/distributions',
    { params: { termId } },
  );
  return data;
}

export async function getDistributionConstraint(
  id: number,
): Promise<DistributionConstraintDto> {
  const { data } = await apiClient.get<DistributionConstraintDto>(
    `/constraints/distributions/${id}`,
  );
  return data;
}

export async function createDistributionConstraint(
  request: CreateDistributionConstraintRequest,
): Promise<DistributionConstraintDto> {
  const { data } = await apiClient.post<DistributionConstraintDto>(
    '/constraints/distributions',
    request,
  );
  return data;
}

export async function updateDistributionConstraint(
  id: number,
  request: CreateDistributionConstraintRequest,
): Promise<DistributionConstraintDto> {
  const { data } = await apiClient.put<DistributionConstraintDto>(
    `/constraints/distributions/${id}`,
    request,
  );
  return data;
}

export async function deleteDistributionConstraint(id: number): Promise<void> {
  await apiClient.delete(`/constraints/distributions/${id}`);
}
