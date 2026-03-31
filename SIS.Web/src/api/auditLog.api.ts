import apiClient from './axios';

export interface AuditLogDto {
  id: number;
  userId: string;
  userName?: string;
  action: string;
  entityType: string;
  entityId: string;
  oldValue?: string;
  newValue?: string;
  timestamp: string;
  ipAddress?: string;
}

export interface AuditLogPagedResult {
  items: AuditLogDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AuditLogFilters {
  entityType?: string;
  userId?: string;
  action?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export async function getAuditLogs(filters: AuditLogFilters): Promise<AuditLogPagedResult> {
  const params = Object.fromEntries(
    Object.entries(filters).filter(([, v]) => v != null && v !== '')
  );
  const { data } = await apiClient.get<AuditLogPagedResult>('/admin/audit-logs', { params });
  return data;
}
