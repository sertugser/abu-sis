import apiClient from './axios';
import { getFilenameFromContentDisposition, type DownloadableFile } from '../lib/download';

export interface SystemConfigDto {
  id: number;
  key: string;
  value: string;
  description: string;
  category: string;
  dataType: string;
}

export interface SystemConfigImportResult {
  updatedCount: number;
  processedCount: number;
}

export async function getSystemConfigs(): Promise<SystemConfigDto[]> {
  const { data } = await apiClient.get<SystemConfigDto[]>('/admin/system-config');
  return data;
}

export async function updateSystemConfig(id: number, value: string): Promise<SystemConfigDto> {
  const { data } = await apiClient.put<SystemConfigDto>(`/admin/system-config/${id}`, { value });
  return data;
}

export async function exportSystemConfigsFile(): Promise<DownloadableFile> {
  const response = await apiClient.get<Blob>('/admin/system-config/export', {
    responseType: 'blob',
  });

  return {
    blob: response.data,
    filename: getFilenameFromContentDisposition(response.headers['content-disposition']),
  };
}

export async function importSystemConfigs(payload: unknown): Promise<SystemConfigImportResult> {
  const { data } = await apiClient.post<SystemConfigImportResult>('/admin/system-config/import', payload);
  return data;
}
