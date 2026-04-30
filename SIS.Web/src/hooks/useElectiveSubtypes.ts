import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { electiveSubtypeApi } from '../api/electiveSubtype.api';
import type {
  CreateElectiveSubtypeRequest,
  UpdateElectiveSubtypeRequest,
} from '../types/electiveSubtype';

const KEYS = {
  list: (includeInactive: boolean) =>
    ['electiveSubtypes', 'list', includeInactive] as const,
};

function invalidateAll(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: ['electiveSubtypes'] });
}

export function useElectiveSubtypeList(includeInactive = false) {
  return useQuery({
    queryKey: KEYS.list(includeInactive),
    queryFn: () => electiveSubtypeApi.list(includeInactive),
    staleTime: 5 * 60 * 1000,
  });
}

export function useCreateElectiveSubtype() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateElectiveSubtypeRequest) => electiveSubtypeApi.create(req),
    onSuccess: () => invalidateAll(qc),
  });
}

export function useUpdateElectiveSubtype() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...req }: { id: number } & UpdateElectiveSubtypeRequest) =>
      electiveSubtypeApi.update(id, req),
    onSuccess: () => invalidateAll(qc),
  });
}
