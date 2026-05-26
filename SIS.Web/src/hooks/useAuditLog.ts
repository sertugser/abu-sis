import { useQuery } from '@tanstack/react-query';
import {
  getAuditLogs, getAuditLogById, getAuditLogAnomalies,
  type AuditLogFilters,
} from '../api/auditLog.api';
import { QUERY_KEYS } from '../lib/constants';

export function useAuditLogs(
  filters: AuditLogFilters,
  options?: { enabled?: boolean; refetchInterval?: number | false },
) {
  return useQuery({
    queryKey: QUERY_KEYS.auditLogs(filters as Record<string, unknown>),
    queryFn: () => getAuditLogs(filters),
    staleTime: 30_000,
    enabled: options?.enabled ?? true,
    refetchInterval: options?.refetchInterval ?? false,
    refetchIntervalInBackground: false,
  });
}

export function useAuditLogDetail(id: number | null) {
  return useQuery({
    queryKey: ['admin', 'auditLogs', 'detail', id] as const,
    queryFn: () => getAuditLogById(id!),
    enabled: id !== null,
    staleTime: 60_000,
  });
}

export function useAuditLogAnomalies() {
  return useQuery({
    queryKey: ['admin', 'auditLogs', 'anomalies'] as const,
    queryFn: getAuditLogAnomalies,
    staleTime: 120_000,
    refetchInterval: 120_000,
  });
}
