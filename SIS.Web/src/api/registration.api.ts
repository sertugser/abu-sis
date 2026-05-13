import apiClient from './axios';
import { toApiError } from '../lib/apiError';
import type {
  RegistrationResponseDto,
  StudentAttendanceStatusDto,
  StudentRegistrationDto,
  CourseHistoryTermDto,
} from '../types/registration';

export async function addToPlan(courseSectionId: number): Promise<RegistrationResponseDto> {
  try {
    const response = await apiClient.post<RegistrationResponseDto>('/registrations/plan', { courseSectionId });
    return response.data;
  } catch (error) {
    throw toApiError(error, 'Plana eklenirken hata olustu.');
  }
}

export async function submitRegistration(courseSectionIds: number[]): Promise<RegistrationResponseDto> {
  try {
    const response = await apiClient.post<RegistrationResponseDto>('/registrations/submit', { courseSectionIds });
    return response.data;
  } catch (error) {
    throw toApiError(error, 'Kayit sirasinda bir hata olustu.');
  }
}

export async function getMyRegistrations(): Promise<StudentRegistrationDto[]> {
  const response = await apiClient.get<StudentRegistrationDto[]>('/registrations/my');
  return response.data;
}

export async function getMyAttendanceStatus(termId?: number | null): Promise<StudentAttendanceStatusDto> {
  const response = await apiClient.get<StudentAttendanceStatusDto>('/registrations/attendance-status', {
    params: termId ? { termId } : undefined,
  });
  return response.data;
}

export async function getCourseHistory(): Promise<CourseHistoryTermDto[]> {
  const response = await apiClient.get<CourseHistoryTermDto[]>('/registrations/history');
  return response.data;
}

/**
 * "Alınan Dersler" PDF dosyasını indir. Backend QuestPDF ile resmî görünüm üretir.
 * Browser yeni sekmede açar veya download tetikler.
 */
export async function downloadCourseHistoryPdf(): Promise<Blob> {
  const response = await apiClient.get<Blob>('/registrations/history/pdf', {
    responseType: 'blob',
  });
  return response.data;
}

export async function dropCourse(registrationId: number): Promise<RegistrationResponseDto> {
  try {
    const response = await apiClient.delete<RegistrationResponseDto>(`/registrations/${registrationId}/drop`);
    return response.data;
  } catch (error) {
    throw toApiError(error, 'Ders plandan kaldirilamadi.');
  }
}

export async function withdrawCourse(registrationId: number): Promise<RegistrationResponseDto> {
  const response = await apiClient.post<RegistrationResponseDto>(`/registrations/${registrationId}/withdraw`);
  return response.data;
}
