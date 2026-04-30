import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getConfigLimits, getSystemConfigs, importSystemConfigs, updateSystemConfig } from '../api/systemConfig.api';
import { QUERY_KEYS } from '../lib/constants';

export function useSystemConfigs() {
  return useQuery({
    queryKey: QUERY_KEYS.systemConfigs,
    queryFn: getSystemConfigs,
    staleTime: 60 * 1000,
  });
}

export function useConfigLimits() {
  return useQuery({
    queryKey: QUERY_KEYS.systemConfigLimits,
    queryFn: getConfigLimits,
    staleTime: 5 * 60 * 1000,
  });
}

export function useUpdateSystemConfig() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, value }: { id: number; value: string }) => updateSystemConfig(id, value),
    onSuccess: () => {
      invalidateRuntimeConfigQueries(qc);
    },
  });
}

export function useImportSystemConfigs() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (payload: unknown) => importSystemConfigs(payload),
    onSuccess: () => {
      invalidateRuntimeConfigQueries(qc);
    },
  });
}

function invalidateRuntimeConfigQueries(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: QUERY_KEYS.systemConfigs });
  qc.invalidateQueries({ queryKey: QUERY_KEYS.studentInfo });
  qc.invalidateQueries({ queryKey: QUERY_KEYS.studentCatalog });
  qc.invalidateQueries({ queryKey: QUERY_KEYS.myRegistrations });
  qc.invalidateQueries({ queryKey: QUERY_KEYS.advisorStudents });
  qc.invalidateQueries({ queryKey: QUERY_KEYS.activeTerm });
  qc.invalidateQueries({ queryKey: ['eligibility'] });
  qc.invalidateQueries({ queryKey: ['recommendation'] });
  qc.invalidateQueries({ queryKey: ['studentAttendanceStatus'] });
  qc.invalidateQueries({ queryKey: ['admin', 'auditLogs'] });
}
