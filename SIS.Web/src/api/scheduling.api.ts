import apiClient from './axios';
import type { ClassroomDashboard, SchedulingConflict, ClassroomFilterOptions, AvailableClassroomResult, AvailableClassroomSearchParams } from '../types/scheduling';
import { getFilenameFromContentDisposition, type DownloadableFile } from '../lib/download';

export interface ClassroomDashboardParams {
  termId?: number;
  facultyId?: number;
  buildingName?: string;
  classroomType?: string;
  minCapacity?: number;
  maxCapacity?: number;
}

export async function getClassroomDashboard(params: ClassroomDashboardParams): Promise<ClassroomDashboard> {
  const { data } = await apiClient.get<ClassroomDashboard>('/scheduling/dashboard', {
    params,
  });
  return data;
}

export async function getSchedulingConflicts(termId?: number): Promise<SchedulingConflict[]> {
  const { data } = await apiClient.get<SchedulingConflict[]>('/scheduling/conflicts', {
    params: { termId }
  });
  return data;
}

export async function getClassroomFilterOptions(termId: number): Promise<ClassroomFilterOptions> {
  const { data } = await apiClient.get<ClassroomFilterOptions>('/scheduling/classroom-filters', {
    params: { termId },
  });
  return data;
}

export async function searchAvailableClassrooms(params: AvailableClassroomSearchParams): Promise<AvailableClassroomResult[]> {
  const { data } = await apiClient.get<AvailableClassroomResult[]>('/scheduling/available-classrooms', {
    params: {
      termId: params.termId,
      days: params.days.join(','),
      startHour: params.startHour,
      endHour: params.endHour,
      minCapacity: params.minCapacity,
      classroomType: params.classroomType,
      facultyId: params.facultyId,
      buildingName: params.buildingName,
    },
  });
  return data;
}

export async function exportClassroomCsv(termId?: number): Promise<DownloadableFile> {
  const response = await apiClient.get<Blob>('/admin/classroom-export/csv', {
    params: { termId },
    responseType: 'blob',
  });

  return {
    blob: response.data,
    filename: getFilenameFromContentDisposition(response.headers['content-disposition']),
  };
}
