import axios from 'axios';
import apiClient from './axios';
import type {
  InstructorAvailability,
  ScheduleSuggestion,
  ScheduleValidationResult,
  UpsertInstructorAvailabilityRequest,
} from '../types/scheduling';

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
    instructorAvailabilities: Array.isArray(data?.instructorAvailabilities) ? data.instructorAvailabilities : [],
  };
}

export async function getInstructorAvailabilities(): Promise<InstructorAvailability[]> {
  const { data } = await apiClient.get('/instructor/availability');
  return Array.isArray(data) ? data : [];
}

export async function createInstructorAvailability(
  payload: UpsertInstructorAvailabilityRequest,
): Promise<InstructorAvailability> {
  const { data } = await apiClient.post('/instructor/availability', normalizeAvailabilityPayload(payload));
  return data;
}

export async function updateInstructorAvailability(
  availabilityId: number,
  payload: UpsertInstructorAvailabilityRequest,
): Promise<InstructorAvailability> {
  const { data } = await apiClient.put(
    `/instructor/availability/${availabilityId}`,
    normalizeAvailabilityPayload(payload),
  );
  return data;
}

export async function deleteInstructorAvailability(availabilityId: number): Promise<void> {
  await apiClient.delete(`/instructor/availability/${availabilityId}`);
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

function normalizeAvailabilityPayload(payload: UpsertInstructorAvailabilityRequest) {
  return {
    dayOfWeek: payload.dayOfWeek,
    startTime: ensureTimeWithSeconds(payload.startTime),
    endTime: ensureTimeWithSeconds(payload.endTime),
    isAvailable: payload.isAvailable,
    isPreferred: payload.isPreferred,
  };
}

function ensureTimeWithSeconds(value: string) {
  if (!value) {
    return value;
  }

  return value.length === 5 ? `${value}:00` : value;
}
