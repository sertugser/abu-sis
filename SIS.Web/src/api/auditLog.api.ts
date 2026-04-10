import apiClient from './axios';
import { getFilenameFromContentDisposition, type DownloadableFile } from '../lib/download';

export interface AuditLogDto {
  id: number;
  userId: number | null;
  userName?: string;
  action: string;
  entityType: string;
  entityId?: string | null;
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
  entityId?: string;
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

export async function exportAuditLogsCsv(filters: AuditLogFilters): Promise<DownloadableFile> {
  const params = Object.fromEntries(
    Object.entries(filters)
      .filter(([, value]) => value != null && value !== '')
      .filter(([key]) => key !== 'page' && key !== 'pageSize')
  );

  const response = await apiClient.get<Blob>('/admin/audit-logs/export/csv', {
    params,
    responseType: 'blob',
  });

  return {
    blob: response.data,
    filename: getFilenameFromContentDisposition(response.headers['content-disposition']),
  };
}
