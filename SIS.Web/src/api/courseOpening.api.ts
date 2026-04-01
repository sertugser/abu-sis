import apiClient from './axios';
import type {
  OpenedCourseListDto,
  CurriculumCourseForOpeningDto,
  InstructorOptionDto,
  InstructorWorkloadDto,
  OpenCourseRequest,
  OpenedCourseSectionDto,
  BulkOpenCoursesRequest,
  BulkOpenCoursesResultDto,
  UpdateOpenedSectionRequest,
} from '../types/courseOpening';

export async function getOpenedCourses(departmentId: number, termId: number): Promise<OpenedCourseListDto> {
  const { data } = await apiClient.get<OpenedCourseListDto>('/course-opening/list', {
    params: { departmentId, termId },
  });
  return data;
}

export async function getCurriculumCoursesForOpening(departmentId: number, termId: number, showAll = false): Promise<CurriculumCourseForOpeningDto[]> {
  const { data } = await apiClient.get<CurriculumCourseForOpeningDto[]>('/course-opening/curriculum-courses', {
    params: { departmentId, termId, showAll },
  });
  return data;
}

export async function getDepartmentInstructors(departmentId: number): Promise<InstructorOptionDto[]> {
  const { data } = await apiClient.get<InstructorOptionDto[]>('/course-opening/instructors', {
    params: { departmentId },
  });
  return data;
}

export async function openCourse(departmentId: number, termId: number, request: OpenCourseRequest): Promise<OpenedCourseSectionDto[]> {
  const { data } = await apiClient.post<OpenedCourseSectionDto[]>('/course-opening/open', request, {
    params: { departmentId, termId },
  });
  return data;
}

export async function bulkOpenCourses(departmentId: number, termId: number, request: BulkOpenCoursesRequest): Promise<BulkOpenCoursesResultDto> {
  const { data } = await apiClient.post<BulkOpenCoursesResultDto>('/course-opening/bulk-open', request, {
    params: { departmentId, termId },
  });
  return data;
}

export async function updateOpenedSection(request: UpdateOpenedSectionRequest): Promise<OpenedCourseSectionDto> {
  const { data } = await apiClient.put<OpenedCourseSectionDto>('/course-opening/section', request);
  return data;
}

export async function closeSection(sectionId: number): Promise<void> {
  await apiClient.delete(`/course-opening/section/${sectionId}`);
}

export async function getInstructorWorkload(instructorId: number, termId: number): Promise<InstructorWorkloadDto> {
  const { data } = await apiClient.get<InstructorWorkloadDto>(
    `/course-opening/instructors/${instructorId}/workload`,
    { params: { termId } },
  );
  return data;
}
