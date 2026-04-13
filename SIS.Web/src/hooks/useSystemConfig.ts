import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getSystemConfigs, importSystemConfigs, updateSystemConfig } from '../api/systemConfig.api';
import { QUERY_KEYS } from '../lib/constants';

export function useSystemConfigs() {
  return useQuery({
    queryKey: QUERY_KEYS.systemConfigs,
    queryFn: getSystemConfigs,
    staleTime: 60 * 1000,
  });
}

export function useUpdateSystemConfig() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, value }: { id: number; value: string }) => updateSystemConfig(id, value),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.systemConfigs });
      qc.invalidateQueries({ queryKey: ['studentAttendanceStatus'] });
      qc.invalidateQueries({ queryKey: ['admin', 'auditLogs'] });
    },
  });
}

export function useImportSystemConfigs() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (payload: unknown) => importSystemConfigs(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.systemConfigs });
      qc.invalidateQueries({ queryKey: ['studentAttendanceStatus'] });
      qc.invalidateQueries({ queryKey: ['admin', 'auditLogs'] });
    },
  });
}
