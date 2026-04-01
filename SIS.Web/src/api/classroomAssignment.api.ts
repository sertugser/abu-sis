import apiClient from './axios';

const API = '/admin/classroom-assignments';

// ── DTOs ────────────────────────────────────────────────────────────────────

export interface ClassroomTermAssignmentDto {
  id: number;
  termId: number;
  classroomId: number;
  classroomName: string;
  facultyId: number;
  facultyName: string;
}

export interface CreateClassroomTermAssignmentDto {
  termId: number;
  classroomId: number;
  facultyId: number;
}

export interface BulkClassroomTermAssignmentDto {
  termId: number;
  assignments: { classroomId: number; facultyId: number }[];
}

export interface BulkAssignmentResultDto {
  created: ClassroomTermAssignmentDto[];
  errors: string[];
}

export interface ClassroomAssignmentSuggestionDto {
  termId: number;
  termName: string;
  totalClassrooms: number;
  alreadyAssigned: number;
  suggestions: ClassroomAssignmentItemDto[];
  facultyLoads: FacultyLoadDto[];
}

export interface ClassroomAssignmentItemDto {
  classroomId: number;
  classroomName: string;
  classroomCapacity: number;
  classroomType: string;
  facultyId: number;
  facultyName: string;
  reason: string;
  priority: string;
}

export interface FacultyLoadDto {
  facultyId: number;
  facultyName: string;
  studentCount: number;
  curriculumCourseCount: number;
  compulsoryCourseCount: number;
  labCourseCount: number;
  amphiNeedCount: number;
  estimatedSectionCount: number;
  totalWeeklyHours: number;
  parallelClassroomNeed: number;
  parallelLabNeed: number;
  historicalSectionCount: number;
  historicalClassroomUsage: number;
  loadPercentage: number;
}

// ── API Functions ───────────────────────────────────────────────────────────

export async function getClassroomAssignments(termId: number): Promise<ClassroomTermAssignmentDto[]> {
  const { data } = await apiClient.get<ClassroomTermAssignmentDto[]>(API, { params: { termId } });
  return data;
}

export async function createClassroomAssignment(dto: CreateClassroomTermAssignmentDto): Promise<ClassroomTermAssignmentDto> {
  const { data } = await apiClient.post<ClassroomTermAssignmentDto>(API, dto);
  return data;
}

export async function deleteClassroomAssignment(id: number): Promise<void> {
  await apiClient.delete(`${API}/${id}`);
}

export async function bulkAssignClassrooms(dto: BulkClassroomTermAssignmentDto): Promise<BulkAssignmentResultDto> {
  const { data } = await apiClient.post<BulkAssignmentResultDto>(`${API}/bulk`, dto);
  return data;
}

export async function suggestClassroomAssignments(termId: number): Promise<ClassroomAssignmentSuggestionDto> {
  const { data } = await apiClient.post<ClassroomAssignmentSuggestionDto>(`${API}/suggest`, null, { params: { termId } });
  return data;
}
