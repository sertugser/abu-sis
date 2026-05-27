import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getWebModuleControlSummaries,
  getWebModuleSettingLimits,
  getWebModuleSettings,
  importWebModuleSettings,
  updateWebModuleControl,
  updateWebModuleSetting,
  type UpdateWebModuleControlPayload,
  type UpdateWebModuleSettingPayload,
} from '../api/webModuleSettings.api';
import { QUERY_KEYS } from '../lib/constants';

export function useWebModuleSettings() {
  return useQuery({
    queryKey: QUERY_KEYS.webModuleSettings,
    queryFn: getWebModuleSettings,
    staleTime: 60 * 1000,
  });
}

export function useWebModuleControlSummaries() {
  return useQuery({
    queryKey: QUERY_KEYS.webModuleControlSummaries,
    queryFn: getWebModuleControlSummaries,
    staleTime: 30 * 1000,
  });
}

export function useWebModuleSettingLimits() {
  return useQuery({
    queryKey: QUERY_KEYS.webModuleSettingLimits,
    queryFn: getWebModuleSettingLimits,
    staleTime: 5 * 60 * 1000,
  });
}

export function useUpdateWebModuleSetting() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: UpdateWebModuleSettingPayload }) => (
      updateWebModuleSetting(id, payload)
    ),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.webModuleSettings });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.webModuleControlSummaries });
      qc.invalidateQueries({ queryKey: ['announcements'] });
      qc.invalidateQueries({ queryKey: ['admin', 'auditLogs'] });
    },
  });
}

export function useUpdateWebModuleControl() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: ({ code, payload }: { code: string; payload: UpdateWebModuleControlPayload }) => (
      updateWebModuleControl(code, payload)
    ),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.webModuleControlSummaries });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.webModuleSettings });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.activeTerm });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentCatalog });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.myRegistrations });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentTranscript });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.termGpaHistory });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentInfo });
      qc.invalidateQueries({ queryKey: ['studentGradeStatistics'] });
      qc.invalidateQueries({ queryKey: ['sectionStudents'] });
      qc.invalidateQueries({ queryKey: ['instructorSections'] });
      qc.invalidateQueries({ queryKey: ['announcements'] });
      qc.invalidateQueries({ queryKey: ['admin', 'auditLogs'] });
    },
  });
}

export function useImportWebModuleSettings() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (payload: unknown) => importWebModuleSettings(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.webModuleSettings });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.webModuleControlSummaries });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.activeTerm });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentCatalog });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.myRegistrations });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentTranscript });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.termGpaHistory });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentInfo });
      qc.invalidateQueries({ queryKey: ['studentGradeStatistics'] });
      qc.invalidateQueries({ queryKey: ['sectionStudents'] });
      qc.invalidateQueries({ queryKey: ['instructorSections'] });
      qc.invalidateQueries({ queryKey: ['announcements'] });
      qc.invalidateQueries({ queryKey: ['admin', 'auditLogs'] });
    },
  });
}
