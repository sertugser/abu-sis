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
  BulkAssignInstructorRequest,
  BulkAssignInstructorResultDto,
  TransferStudentsToSectionRequest,
  TransferStudentsToSectionResultDto,
  AutoSplitCourseRequest,
  AutoSplitCourseResultDto,
  PreviousTermCourseDto,
  TransferFromTermRequest,
  TransferFromTermResultDto,
  TransferDefaultsDto,
  PoolSlotForOpeningDto,
  PoolCandidateDto,
} from '../types/courseOpening';

export async function getOpenedCourses(departmentId: number, termId: number): Promise<OpenedCourseListDto> {
  const { data } = await apiClient.get<OpenedCourseListDto>('/course-opening/list', {
    params: { departmentId, termId },
  });
  return data;
}

export interface CurriculumCoursesQueryOptions {
  showAll?: boolean;
  curriculumId?: number | null;
  includeUnmapped?: boolean;
}

export async function getCurriculumCoursesForOpening(
  departmentId: number,
  termId: number,
  options: CurriculumCoursesQueryOptions = {},
): Promise<CurriculumCourseForOpeningDto[]> {
  const params: Record<string, unknown> = {
    departmentId,
    termId,
    showAll: options.showAll ?? false,
    includeUnmapped: options.includeUnmapped ?? false,
  };
  if (options.curriculumId != null) params.curriculumId = options.curriculumId;
  const { data } = await apiClient.get<CurriculumCourseForOpeningDto[]>('/course-opening/curriculum-courses', { params });
  return data;
}

export async function getPoolSlotsForOpening(curriculumId: number, termId: number): Promise<PoolSlotForOpeningDto[]> {
  const { data } = await apiClient.get<PoolSlotForOpeningDto[]>('/course-opening/pool-slots', {
    params: { curriculumId, termId },
  });
  return data;
}

export async function getPoolCandidates(poolId: number, termId: number): Promise<PoolCandidateDto[]> {
  const { data } = await apiClient.get<PoolCandidateDto[]>('/course-opening/pool-candidates', {
    params: { poolId, termId },
  });
  return data;
}

export async function getDepartmentInstructors(departmentId?: number | null, termId?: number): Promise<InstructorOptionDto[]> {
  const { data } = await apiClient.get<InstructorOptionDto[]>('/course-opening/instructors', {
    params: { departmentId: departmentId ?? undefined, termId },
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

export async function closeAllSections(courseId: number, termId: number): Promise<{ closedSectionCount: number; droppedRegistrationCount: number }> {
  const { data } = await apiClient.delete(`/course-opening/course/${courseId}`, { params: { termId } });
  return data;
}

export async function deleteAllSectionsHard(
  courseId: number,
  termId: number,
): Promise<{ deletedSectionCount: number; deletedRegistrationCount: number; deletedAccessPolicyCount: number }> {
  const { data } = await apiClient.delete(`/course-opening/course/${courseId}/hard`, { params: { termId } });
  return data;
}

export async function reactivateSection(sectionId: number): Promise<void> {
  await apiClient.put(`/course-opening/section/${sectionId}/reactivate`);
}

export async function bulkAssignInstructor(
  departmentId: number,
  termId: number,
  request: BulkAssignInstructorRequest,
): Promise<BulkAssignInstructorResultDto> {
  const { data } = await apiClient.post<BulkAssignInstructorResultDto>(
    '/course-opening/bulk-assign-instructor',
    request,
    { params: { departmentId, termId } },
  );
  return data;
}

export async function transferStudentsBetweenSections(
  request: TransferStudentsToSectionRequest,
): Promise<TransferStudentsToSectionResultDto> {
  const { data } = await apiClient.post<TransferStudentsToSectionResultDto>(
    '/course-opening/transfer-students',
    request,
  );
  return data;
}

export async function autoSplitCourse(request: AutoSplitCourseRequest): Promise<AutoSplitCourseResultDto> {
  const { data } = await apiClient.post<AutoSplitCourseResultDto>('/course-opening/auto-split', request);
  return data;
}

export interface PreviousTermCoursesFilter {
  programId?: number | null;
  classLevel?: number | null;
}

export async function getPreviousTermCourses(
  departmentId: number,
  sourceTermId: number,
  targetTermId: number,
  filter?: PreviousTermCoursesFilter,
): Promise<PreviousTermCourseDto[]> {
  const { data } = await apiClient.get<PreviousTermCourseDto[]>('/course-opening/previous-term-courses', {
    params: {
      departmentId,
      sourceTermId,
      targetTermId,
      programId: filter?.programId ?? undefined,
      classLevel: filter?.classLevel ?? undefined,
    },
  });
  return data;
}

export async function transferFromTerm(request: TransferFromTermRequest): Promise<TransferFromTermResultDto> {
  const { data } = await apiClient.post<TransferFromTermResultDto>('/course-opening/transfer-from-term', request);
  return data;
}

export async function getTransferDefaults(): Promise<TransferDefaultsDto> {
  const { data } = await apiClient.get<TransferDefaultsDto>('/course-opening/transfer-defaults');
  return data;
}

export interface ExternalCourseDto {
  courseId: number;
  courseCode: string;
  courseName: string;
  ects: number;
  ownerDepartmentName: string;
  sectionCount: number;
  totalCapacity: number;
  totalEnrolled: number;
  predictedDemand: number;
}

export async function getExternalCourses(departmentId: number, termId: number): Promise<ExternalCourseDto[]> {
  const { data } = await apiClient.get<ExternalCourseDto[]>('/course-opening/external-courses', {
    params: { departmentId, termId },
  });
  return data;
}

export async function getInstructorWorkload(
  instructorId: number,
  termId: number,
  departmentId?: number | null,
): Promise<InstructorWorkloadDto> {
  const { data } = await apiClient.get<InstructorWorkloadDto>(
    `/course-opening/instructors/${instructorId}/workload`,
    { params: { termId, departmentId: departmentId ?? undefined } },
  );
  return data;
}
