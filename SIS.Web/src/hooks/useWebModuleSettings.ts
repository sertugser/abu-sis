import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getWebModuleSettingLimits,
  getWebModuleSettings,
  importWebModuleSettings,
  updateWebModuleSetting,
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
      qc.invalidateQueries({ queryKey: QUERY_KEYS.activeTerm });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentInfo });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.myRegistrations });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentCatalog });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentAcademicCalendarActive });
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
      qc.invalidateQueries({ queryKey: QUERY_KEYS.activeTerm });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentInfo });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.myRegistrations });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentCatalog });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentAcademicCalendarActive });
      qc.invalidateQueries({ queryKey: ['admin', 'auditLogs'] });
    },
  });
}
