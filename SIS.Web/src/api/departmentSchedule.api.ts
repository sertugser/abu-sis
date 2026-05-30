import apiClient from './axios';
import type {
  DepartmentSchedule,
  DepartmentScheduleContext,
} from '@/types/departmentSchedule';

export async function getDepartmentScheduleContext(): Promise<DepartmentScheduleContext> {
  const { data } = await apiClient.get<DepartmentScheduleContext>(
    '/scheduling/department/context',
  );
  return data;
}

export interface DepartmentScheduleQuery {
  termId: number;
  programId: number;
  classYear: number;
}

export async function getDepartmentSchedule(query: DepartmentScheduleQuery): Promise<DepartmentSchedule> {
  const { data } = await apiClient.get<DepartmentSchedule>(
    '/scheduling/department/schedule',
    { params: query },
  );
  return data;
}

export async function downloadDepartmentSchedulePdf(query: DepartmentScheduleQuery): Promise<Blob> {
  const response = await apiClient.get<Blob>(
    '/scheduling/department/schedule/pdf',
    { params: query, responseType: 'blob' },
  );
  return response.data;
}
