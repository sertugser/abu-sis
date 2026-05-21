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
      qc.invalidateQueries({ queryKey: QUERY_KEYS.systemConfigs });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.activeTerm });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentCatalog });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentTranscript });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.termGpaHistory });
      qc.invalidateQueries({ queryKey: ['studentGradeStatistics'] });
      qc.invalidateQueries({ queryKey: ['grades'] });
      qc.invalidateQueries({ queryKey: ['advisor'] });
      qc.invalidateQueries({ queryKey: ['sectionStudents'] });
      qc.invalidateQueries({ queryKey: ['sectionDetail'] });
      qc.invalidateQueries({ queryKey: ['instructorSections'] });
      qc.invalidateQueries({ queryKey: ['instructorProfile'] });
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
      qc.invalidateQueries({ queryKey: QUERY_KEYS.activeTerm });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentCatalog });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentTranscript });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.termGpaHistory });
      qc.invalidateQueries({ queryKey: ['studentGradeStatistics'] });
      qc.invalidateQueries({ queryKey: ['grades'] });
      qc.invalidateQueries({ queryKey: ['advisor'] });
      qc.invalidateQueries({ queryKey: ['sectionStudents'] });
      qc.invalidateQueries({ queryKey: ['sectionDetail'] });
      qc.invalidateQueries({ queryKey: ['instructorSections'] });
      qc.invalidateQueries({ queryKey: ['instructorProfile'] });
      qc.invalidateQueries({ queryKey: ['studentAttendanceStatus'] });
      qc.invalidateQueries({ queryKey: ['admin', 'auditLogs'] });
    },
  });
}
