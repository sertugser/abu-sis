import apiClient from './axios';
import { getFilenameFromContentDisposition, type DownloadableFile } from '../lib/download';

/* ── Summary DTO (list endpoint) ── */
export interface AuditLogSummaryDto {
  id: number;
  userId: number | null;
  userName?: string;
  action: string;
  entityType: string;
  entityId?: string | null;
  oldValue?: string | null;
  newValue?: string | null;
  timestamp: string;
  ipAddress?: string;
}

/* ── Detail DTO (single-item endpoint — includes oldValue/newValue) ── */
export interface AuditLogDetailDto {
  id: number;
  userId: number | null;
  userName?: string;
  action: string;
  entityType: string;
  entityId?: string | null;
  oldValue?: string | null;
  newValue?: string | null;
  timestamp: string;
  ipAddress?: string;
}

/* ── Backward compat alias ── */
export type AuditLogDto = AuditLogSummaryDto;

export interface AuditLogPagedResult {
  items: AuditLogSummaryDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AuditLogFilters {
  entityType?: string;
  entityId?: string;
  userId?: string;
  action?: string;
  search?: string;
  from?: string;
  to?: string;
  sortBy?: string;
  sortDir?: string;
  page?: number;
  pageSize?: number;
}

export interface AuditLogStatsDto {
  todayTotal: number;
  todayFailedLogins: number;
  todayErrors: number;
  todayUnauthorized: number;
  last7DaysTotal: number;
}

export interface AuditLogAnomalyDto {
  type: string;
  message: string;
  severity: 'warning' | 'critical';
  count: number;
  detectedAt: string;
}

/* ── API Functions ── */

export async function getAuditLogs(filters: AuditLogFilters): Promise<AuditLogPagedResult> {
  const params = Object.fromEntries(
    Object.entries(filters).filter(([, v]) => v != null && v !== '')
  );
  const { data } = await apiClient.get<AuditLogPagedResult>('/admin/audit-logs', { params });
  return data;
}

export async function getAuditLogById(id: number): Promise<AuditLogDetailDto> {
  const { data } = await apiClient.get<AuditLogDetailDto>(`/admin/audit-logs/${id}`);
  return data;
}

export async function getAuditLogStats(): Promise<AuditLogStatsDto> {
  const { data } = await apiClient.get<AuditLogStatsDto>('/admin/audit-logs/stats');
  return data;
}

export async function getAuditLogAnomalies(): Promise<AuditLogAnomalyDto[]> {
  const { data } = await apiClient.get<AuditLogAnomalyDto[]>('/admin/audit-logs/anomalies');
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
