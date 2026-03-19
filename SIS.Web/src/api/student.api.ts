import apiClient from './axios';
import type { StudentInfoDto } from '../types/student';
import type { CurriculumStatusDto } from '../types/curriculum';

export async function getStudentInfo(): Promise<StudentInfoDto> {
  const response = await apiClient.get<StudentInfoDto>('/student/me');
  return response.data;
}

export async function getCurriculumStatus(): Promise<CurriculumStatusDto> {
  const response = await apiClient.get<CurriculumStatusDto>('/student/curriculum-status');
  return response.data;
}
