import apiClient from './axios';
import type { CourseListDto, StudentCourseDto, ActiveTermDto, SectionDetailDto } from '../types/course';

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
  } catch (err: unknown) {
    if (err && typeof err === 'object' && 'response' in err) {
      const axiosError = err as { response?: { status?: number; data?: { error?: string } } };
      const message = axiosError.response?.data?.error;
      if (message) {
        const enriched = new Error(message) as Error & { statusCode?: number };
        enriched.statusCode = axiosError.response?.status;
        throw enriched;
      }
    }
    throw err;
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
