import apiClient from './axios';
import type { RegistrationResponseDto, StudentRegistrationDto, CourseHistoryTermDto } from '../types/registration';

export async function addToPlan(courseSectionId: number): Promise<RegistrationResponseDto> {
  const response = await apiClient.post<RegistrationResponseDto>('/registrations/plan', { courseSectionId });
  return response.data;
}

export async function submitRegistration(courseSectionIds: number[]): Promise<RegistrationResponseDto> {
  const response = await apiClient.post<RegistrationResponseDto>('/registrations/submit', { courseSectionIds });
  return response.data;
}

export async function getMyRegistrations(): Promise<StudentRegistrationDto[]> {
  const response = await apiClient.get<StudentRegistrationDto[]>('/registrations/my');
  return response.data;
}

export async function getCourseHistory(): Promise<CourseHistoryTermDto[]> {
  const response = await apiClient.get<CourseHistoryTermDto[]>('/registrations/history');
  return response.data;
}

export async function dropCourse(registrationId: number): Promise<RegistrationResponseDto> {
  const response = await apiClient.delete<RegistrationResponseDto>(`/registrations/${registrationId}/drop`);
  return response.data;
}

export async function withdrawCourse(registrationId: number): Promise<RegistrationResponseDto> {
  const response = await apiClient.post<RegistrationResponseDto>(`/registrations/${registrationId}/withdraw`);
  return response.data;
}
