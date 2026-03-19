import apiClient from './axios';
import type { CourseListDto, StudentCourseDto, ActiveTermDto, SectionDetailDto } from '../types/course';

export async function getCourses(programId?: number): Promise<CourseListDto[]> {
  const response = await apiClient.get<CourseListDto[]>('/course', {
    params: programId ? { programId } : undefined,
  });
  return response.data;
}

export async function getStudentCatalog(): Promise<StudentCourseDto[]> {
  const response = await apiClient.get<StudentCourseDto[]>('/course/student-catalog');
  return response.data;
}

export async function getSectionDetail(sectionId: number): Promise<SectionDetailDto> {
  const response = await apiClient.get<SectionDetailDto>(`/course/sections/${sectionId}`);
  return response.data;
}

export async function getActiveTerm(): Promise<ActiveTermDto> {
  const response = await apiClient.get<ActiveTermDto>('/course/terms/active');
  return response.data;
}
