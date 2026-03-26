import apiClient from './axios';
import type { DepartmentForecastSummaryDto } from '../types/forecast';

export async function getDepartmentForecastSummary(departmentId: number): Promise<DepartmentForecastSummaryDto> {
  const { data } = await apiClient.get<DepartmentForecastSummaryDto>('/forecast/department-summary', {
    params: { departmentId },
  });
  return data;
}
