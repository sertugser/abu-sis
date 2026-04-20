import axios from 'axios';
import apiClient from './axios';
import type { BatchAutoScheduleRequest, BatchAutoScheduleResult, ClassroomDashboard, SchedulingConflict, ClassroomFilterOptions, AvailableClassroomResult, AvailableClassroomSearchParams } from '../types/scheduling';
import { getFilenameFromContentDisposition, type DownloadableFile } from '../lib/download';

export async function batchAutoSchedule(request: BatchAutoScheduleRequest): Promise<BatchAutoScheduleResult> {
  const { data } = await apiClient.post('/scheduling/batch-auto-schedule', request);
  return {
    totalSections: typeof data?.totalSections === 'number' ? data.totalSections : 0,
    scheduledCount: typeof data?.scheduledCount === 'number' ? data.scheduledCount : 0,
    failedCount: typeof data?.failedCount === 'number' ? data.failedCount : 0,
    skippedCount: typeof data?.skippedCount === 'number' ? data.skippedCount : 0,
    results: Array.isArray(data?.results) ? data.results : [],
    notes: Array.isArray(data?.notes) ? data.notes : [],
  };
}

export function getSchedulingManagementErrorMessage(error: unknown, fallbackMessage: string) {
  if (!axios.isAxiosError(error)) {
    return fallbackMessage;
  }

  const status = error.response?.status;
  const apiMessage = typeof error.response?.data?.error === 'string'
    ? error.response.data.error
    : undefined;

  if (status === 403) {
    return 'Seçilen bölüm için toplu programlama yetkiniz bulunmuyor.';
  }

  if (status === 404) {
    return apiMessage ?? 'İstenen programlama kaynağı bulunamadı.';
  }

  if (status === 400) {
    return apiMessage ?? fallbackMessage;
  }

  if (status && status >= 500) {
    return 'Toplu programlama sırasında sunucu hatası oluştu. Lütfen tekrar deneyin.';
  }

  return apiMessage ?? fallbackMessage;
}

export interface ClassroomDashboardParams {
  termId?: number;
  facultyId?: number;
  buildingName?: string;
  classroomType?: string;
  minCapacity?: number;
  maxCapacity?: number;
}

export async function getClassroomDashboard(params: ClassroomDashboardParams): Promise<ClassroomDashboard> {
  const { data } = await apiClient.get<ClassroomDashboard>('/scheduling/dashboard', {
    params,
  });
  return data;
}

export async function getSchedulingConflicts(termId?: number): Promise<SchedulingConflict[]> {
  const { data } = await apiClient.get<SchedulingConflict[]>('/scheduling/conflicts', {
    params: { termId }
  });
  return data;
}

export async function getClassroomFilterOptions(termId: number): Promise<ClassroomFilterOptions> {
  const { data } = await apiClient.get<ClassroomFilterOptions>('/scheduling/classroom-filters', {
    params: { termId },
  });
  return data;
}

export async function searchAvailableClassrooms(params: AvailableClassroomSearchParams): Promise<AvailableClassroomResult[]> {
  const { data } = await apiClient.get<AvailableClassroomResult[]>('/scheduling/available-classrooms', {
    params: {
      termId: params.termId,
      days: params.days.join(','),
      startHour: params.startHour,
      endHour: params.endHour,
      minCapacity: params.minCapacity,
      classroomType: params.classroomType,
      facultyId: params.facultyId,
      buildingName: params.buildingName,
    },
  });
  return data;
}

export async function exportClassroomCsv(termId?: number): Promise<DownloadableFile> {
  const response = await apiClient.get<Blob>('/admin/classroom-export/csv', {
    params: { termId },
    responseType: 'blob',
  });

  return {
    blob: response.data,
    filename: getFilenameFromContentDisposition(response.headers['content-disposition']),
  };
}
