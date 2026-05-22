import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  delegationApi,
  type CreateDelegationDto,
  type ListDelegationsParams,
  type RevokeDelegationDto,
} from '@/api/delegation.api';

const baseKey = ['admin', 'delegations'] as const;

export function useDelegations(params: ListDelegationsParams = {}) {
  return useQuery({
    queryKey: [...baseKey, 'list', params],
    queryFn: () => delegationApi.list(params),
    staleTime: 30_000,
  });
}

function invalidateAll(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: [...baseKey] });
  // Vekâlet permission union'ı + effective-roles + sidebar menü için ilgili cache'leri yenile
  qc.invalidateQueries({ queryKey: ['my-permissions'] });
  qc.invalidateQueries({ queryKey: ['menu'] });
  qc.invalidateQueries({ queryKey: ['me', 'effective-roles'] });
}

export function useCreateDelegation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: CreateDelegationDto) => delegationApi.create(dto),
    onSuccess: () => invalidateAll(qc),
  });
}

export function useRevokeDelegation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: number; dto: RevokeDelegationDto }) =>
      delegationApi.revoke(id, dto),
    onSuccess: () => invalidateAll(qc),
  });
}
