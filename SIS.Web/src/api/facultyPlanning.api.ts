import apiClient from './axios';
import type {
  ApplyScheduleRequest, ApplyScheduleResult, AssignmentResult, AssignSectionRequest,
  AutoSchedulePreview, AutoScheduleRequest, AvailableClassroom,
  FacultyPlanningOverview, FacultyPlanningSectionPage,
} from '../types/facultyPlanning';
import type { SchedulingConflict } from '../types/scheduling';

export async function getFacultyPlanningOverview(facultyId: number, termId: number): Promise<FacultyPlanningOverview> {
  const { data } = await apiClient.get<FacultyPlanningOverview>('/faculty-planning/overview', {
    params: { facultyId, termId },
  });
  return data;
}

export async function getFacultyPlanningSections(
  facultyId: number, termId: number, departmentId?: number, status?: string, page = 1, pageSize = 50,
): Promise<FacultyPlanningSectionPage> {
  const { data } = await apiClient.get<FacultyPlanningSectionPage>('/faculty-planning/sections', {
    params: { facultyId, termId, departmentId, status, page, pageSize },
  });
  return data;
}

export async function assignSection(request: AssignSectionRequest): Promise<AssignmentResult> {
  const { data } = await apiClient.post<AssignmentResult>('/faculty-planning/assign', request);
  return data;
}

export async function unassignSection(sectionId: number): Promise<void> {
  await apiClient.delete(`/faculty-planning/unassign/${sectionId}`);
}

export async function autoSchedulePreview(request: AutoScheduleRequest): Promise<AutoSchedulePreview> {
  const { data } = await apiClient.post<AutoSchedulePreview>('/faculty-planning/auto-schedule', request);
  return data;
}

export async function applySchedule(request: ApplyScheduleRequest): Promise<ApplyScheduleResult> {
  const { data } = await apiClient.post<ApplyScheduleResult>('/faculty-planning/apply-schedule', request);
  return data;
}

export async function getAvailableClassrooms(
  termId: number, day?: number, startTime?: string, endTime?: string, minCapacity?: number, type?: string,
): Promise<AvailableClassroom[]> {
  const { data } = await apiClient.get<AvailableClassroom[]>('/faculty-planning/available-classrooms', {
    params: { termId, day, startTime, endTime, minCapacity, type },
  });
  return data;
}

export async function getFacultyPlanningConflicts(facultyId: number, termId: number): Promise<SchedulingConflict[]> {
  const { data } = await apiClient.get<SchedulingConflict[]>('/faculty-planning/conflicts', {
    params: { facultyId, termId },
  });
  return data;
}
