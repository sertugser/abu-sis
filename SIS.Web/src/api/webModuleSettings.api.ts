import apiClient from './axios';
import { getFilenameFromContentDisposition, type DownloadableFile } from '../lib/download';

export interface WebModuleSettingDto {
  id: number;
  moduleKey: string;
  key: string;
  value: string;
  description: string | null;
  category: string;
  dataType: string;
  effectiveFrom: string | null;
  effectiveTo: string | null;
}

export interface UpdateWebModuleSettingPayload {
  value: string;
  effectiveFrom: string | null;
  effectiveTo: string | null;
}

export interface WebModuleSettingImportResult {
  updatedCount: number;
  processedCount: number;
}

export interface WebModuleSettingLimits {
  [key: string]: { min: number | null; max: number | null };
}

export async function getWebModuleSettings(): Promise<WebModuleSettingDto[]> {
  const { data } = await apiClient.get<WebModuleSettingDto[]>('/admin/web-module-settings');
  return data;
}

export async function updateWebModuleSetting(
  id: number,
  payload: UpdateWebModuleSettingPayload,
): Promise<WebModuleSettingDto> {
  const { data } = await apiClient.put<WebModuleSettingDto>(`/admin/web-module-settings/${id}`, payload);
  return data;
}

export async function exportWebModuleSettingsFile(): Promise<DownloadableFile> {
  const response = await apiClient.get<Blob>('/admin/web-module-settings/export', {
    responseType: 'blob',
  });

  return {
    blob: response.data,
    filename: getFilenameFromContentDisposition(response.headers['content-disposition']),
  };
}

export async function importWebModuleSettings(payload: unknown): Promise<WebModuleSettingImportResult> {
  const { data } = await apiClient.post<WebModuleSettingImportResult>('/admin/web-module-settings/import', payload);
  return data;
}

export async function getWebModuleSettingLimits(): Promise<WebModuleSettingLimits> {
  const { data } = await apiClient.get<WebModuleSettingLimits>('/admin/web-module-settings/limits');
  return data;
}
