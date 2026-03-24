import axios from 'axios';
import apiClient from './axios';
import type { BatchAutoScheduleRequest, BatchAutoScheduleResult } from '../types/scheduling';

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
