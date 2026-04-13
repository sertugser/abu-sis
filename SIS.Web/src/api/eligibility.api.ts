import apiClient from './axios';
import type { StudentCourseEligibilityDto } from '../types/eligibility';

export async function getStudentEligibility(studentId: number): Promise<StudentCourseEligibilityDto[]> {
  const response = await apiClient.get<StudentCourseEligibilityDto[]>(`/Eligibility/student/${studentId}`);
  return response.data;
}
