import apiClient from './axios';
import type { ImportPreviewDto, ImportResultDto } from '../types/scheduleExcel';

export async function exportScheduleExcel(facultyId: number, termId: number): Promise<Blob> {
  const { data } = await apiClient.get<Blob>('/schedule-excel/export', {
    params: { facultyId, termId },
    responseType: 'blob',
  });
  return data;
}

export async function importSchedulePreview(
  facultyId: number,
  termId: number,
  file: File,
): Promise<ImportPreviewDto> {
  const formData = new FormData();
  formData.append('file', file);
  formData.append('facultyId', String(facultyId));
  formData.append('termId', String(termId));

  const { data } = await apiClient.post<ImportPreviewDto>(
    '/schedule-excel/import-preview',
    formData,
    { headers: { 'Content-Type': 'multipart/form-data' } },
  );
  return data;
}

export async function importScheduleApply(
  facultyId: number,
  termId: number,
  importSessionId: string,
  rowNumbers: number[],
): Promise<ImportResultDto> {
  const { data } = await apiClient.post<ImportResultDto>('/schedule-excel/import-apply', {
    facultyId,
    termId,
    importSessionId,
    rowNumbers,
  });
  return data;
}

export async function exportSchedulePdf(termId: number, facultyId: number, departmentId?: number): Promise<Blob> {
  const params = new URLSearchParams({ termId: String(termId), facultyId: String(facultyId) });
  if (departmentId) params.set('departmentId', String(departmentId));
  const res = await apiClient.get(`/schedule-excel/export-pdf?${params}`, { responseType: 'blob' });
  return res.data;
}
