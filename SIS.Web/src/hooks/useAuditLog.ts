import { useQuery } from '@tanstack/react-query';
import { getAuditLogs, type AuditLogFilters } from '../api/auditLog.api';
import { QUERY_KEYS } from '../lib/constants';

export function useAuditLogs(filters: AuditLogFilters, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: QUERY_KEYS.auditLogs(filters as Record<string, unknown>),
    queryFn: () => getAuditLogs(filters),
    staleTime: 30 * 1000,
    enabled: options?.enabled ?? true,
  });
}
