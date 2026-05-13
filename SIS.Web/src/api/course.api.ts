import apiClient from './axios';
import { toApiError } from '../lib/apiError';
import type { CourseListDto, StudentCourseDto, ActiveTermDto, SectionDetailDto, CourseDetailDto } from '../types/course';

export async function getCourses(programId?: number): Promise<CourseListDto[]> {
  const response = await apiClient.get<CourseListDto[]>('/course', {
    params: programId ? { programId } : undefined,
  });
  return response.data;
}

export async function getStudentCatalog(): Promise<StudentCourseDto[]> {
  try {
    const response = await apiClient.get<StudentCourseDto[]>('/course/student-catalog');
    return response.data;
  } catch (error) {
    throw toApiError(error, 'Ders katalogu yuklenemedi.');
  }
}

export async function getSectionDetail(sectionId: number): Promise<SectionDetailDto> {
  const response = await apiClient.get<SectionDetailDto>(`/course/sections/${sectionId}`);
  return response.data;
}

export async function getActiveTerm(): Promise<ActiveTermDto> {
  const response = await apiClient.get<ActiveTermDto>('/course/terms/active');
  return response.data;
}

/**
 * Ders katalog detayı — "Alınan Dersler" sayfasındaki "Ders Bilgileri" sheet'i için.
 * Course tüm alanları + Prerequisites + Equivalencies + Department/Faculty.
 */
export async function getCourseDetail(courseId: number): Promise<CourseDetailDto> {
  const response = await apiClient.get<CourseDetailDto>(`/course/${courseId}/detail`);
  return response.data;
}
