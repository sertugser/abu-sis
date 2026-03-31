import axios from 'axios';
import apiClient from './axios';
import type {
  ApplyScheduleRequest,
  ScheduleSuggestion,
  ScheduleValidationResult,
} from '../types/scheduling';

export interface TimetableEntry {
  day: number;
  startTime: string;
  endTime: string;
  courseCode: string;
  courseName: string;
  classroomName: string | null;
  sectionId: number;
  sectionNumber?: number;
  instructorName?: string;
}

export async function getInstructorTimetable(
  instructorId: number,
  termId?: number,
): Promise<TimetableEntry[]> {
  const params: Record<string, number> = {};
  if (termId) {
    params.termId = termId;
  }
  const { data } = await apiClient.get(`/scheduling/instructor/${instructorId}/timetable`, { params });
  return Array.isArray(data) ? data : [];
}

export async function validateInstructorSectionSchedule(sectionId: number): Promise<ScheduleValidationResult> {
  const { data } = await apiClient.post(`/instructor/section/${sectionId}/schedule/validate`, {});
  return {
    isValid: Boolean(data?.isValid),
    conflicts: Array.isArray(data?.conflicts) ? data.conflicts : [],
    notes: Array.isArray(data?.notes) ? data.notes : [],
  };
}

export async function suggestInstructorSectionSchedule(sectionId: number): Promise<ScheduleSuggestion> {
  const { data } = await apiClient.post(`/instructor/section/${sectionId}/schedule/suggest`, {});
  return {
    isFeasible: Boolean(data?.isFeasible),
    sectionId: typeof data?.sectionId === 'number' ? data.sectionId : sectionId,
    instructorId: typeof data?.instructorId === 'number' ? data.instructorId : null,
    instructorName: typeof data?.instructorName === 'string' ? data.instructorName : '',
    suggestedClassroomId: typeof data?.suggestedClassroomId === 'number' ? data.suggestedClassroomId : null,
    suggestedClassroomName: typeof data?.suggestedClassroomName === 'string' ? data.suggestedClassroomName : '',
    suggestedSlots: Array.isArray(data?.suggestedSlots) ? data.suggestedSlots : [],
    estimatedWeeklyHoursImpact: typeof data?.estimatedWeeklyHoursImpact === 'number' ? data.estimatedWeeklyHoursImpact : 0,
    notes: Array.isArray(data?.notes) ? data.notes : [],
  };
}

export async function applyInstructorSectionSchedule(
  sectionId: number,
  request: ApplyScheduleRequest,
): Promise<ScheduleValidationResult> {
  const { data } = await apiClient.post(`/instructor/section/${sectionId}/schedule/apply`, request);
  return {
    isValid: Boolean(data?.isValid),
    conflicts: Array.isArray(data?.conflicts) ? data.conflicts : [],
    notes: Array.isArray(data?.notes) ? data.notes : [],
  };
}

export function getSchedulingErrorMessage(error: unknown, fallbackMessage: string): string {
  if (!axios.isAxiosError(error)) {
    return fallbackMessage;
  }

  const status = error.response?.status;
  const apiMessage = typeof error.response?.data?.error === 'string'
    ? error.response.data.error
    : undefined;

  if (status === 403) {
    return 'Bu işlem için yetkiniz bulunmuyor.';
  }

  if (status === 404) {
    if (!apiMessage && fallbackMessage === 'Program uygulanamadı.') {
      return 'Program uygulama endpointi bulunamadı. API sunucusunu yeniden başlatıp tekrar deneyin.';
    }

    return apiMessage ?? 'Seçilen ders bulunamadı veya size ait değil.';
  }

  if (status === 400) {
    return apiMessage ?? 'Gönderilen program bilgisi doğrulanamadı.';
  }

  if (status && status >= 500) {
    return 'Program bilgileri alınırken sunucu hatası oluştu. Lütfen biraz sonra tekrar deneyin.';
  }

  return apiMessage ?? fallbackMessage;
}
