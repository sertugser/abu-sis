import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getDistributionConstraints,
  createDistributionConstraint,
  updateDistributionConstraint,
  deleteDistributionConstraint,
} from '../api/distributionConstraint.api';
import { QUERY_KEYS } from '../lib/constants';
import type { CreateDistributionConstraintRequest } from '../types/distributionConstraint';

export function useDistributionConstraints(termId: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.distributionConstraints(termId!),
    queryFn: () => getDistributionConstraints(termId!),
    enabled: termId != null && termId > 0,
    staleTime: 30 * 1000,
  });
}

export function useCreateDistributionConstraint() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateDistributionConstraintRequest) =>
      createDistributionConstraint(request),
    onSuccess: (_data, variables) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.distributionConstraints(variables.termId) });
    },
  });
}

export function useUpdateDistributionConstraint() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: number; request: CreateDistributionConstraintRequest }) =>
      updateDistributionConstraint(id, request),
    onSuccess: (_data, variables) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.distributionConstraints(variables.request.termId) });
    },
  });
}

export function useDeleteDistributionConstraint() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id }: { id: number; termId: number }) =>
      deleteDistributionConstraint(id),
    onSuccess: (_data, variables) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.distributionConstraints(variables.termId) });
    },
  });
}
