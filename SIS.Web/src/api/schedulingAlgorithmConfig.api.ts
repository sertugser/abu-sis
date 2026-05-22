import apiClient from './axios';

export interface SchedulingAlgorithmConfigDto {
  id: number;
  key: string;
  value: string;
  description: string;
  category: string;
  dataType: string;
}

export interface SchedulingAlgorithmConfigLimits {
  [key: string]: { min: number | null; max: number | null };
}

export interface SchedulingAlgorithmConfigGroupDto {
  category: string;
  displayName: string;
  description: string | null;
  badgeClass: string | null;
  sortOrder: number;
}

export interface EnumOptionDto {
  value: string;
  label: string;
  disabled: boolean;
  helpText: string | null;
}

export interface SchedulingAlgorithmConfigEnumOptions {
  [configKey: string]: EnumOptionDto[];
}

const BASE = '/admin/scheduling-algorithm-config';

export async function getSchedulingAlgorithmConfigs(): Promise<SchedulingAlgorithmConfigDto[]> {
  const { data } = await apiClient.get<SchedulingAlgorithmConfigDto[]>(BASE);
  return data;
}

export async function getSchedulingAlgorithmConfigGroups(): Promise<SchedulingAlgorithmConfigGroupDto[]> {
  const { data } = await apiClient.get<SchedulingAlgorithmConfigGroupDto[]>(`${BASE}/groups`);
  return data;
}

export async function getSchedulingAlgorithmConfigLimits(): Promise<SchedulingAlgorithmConfigLimits> {
  const { data } = await apiClient.get<SchedulingAlgorithmConfigLimits>(`${BASE}/limits`);
  return data;
}

export async function getSchedulingAlgorithmConfigEnumOptions(): Promise<SchedulingAlgorithmConfigEnumOptions> {
  const { data } = await apiClient.get<SchedulingAlgorithmConfigEnumOptions>(`${BASE}/enum-options`);
  return data;
}

export async function updateSchedulingAlgorithmConfig(id: number, value: string): Promise<SchedulingAlgorithmConfigDto> {
  const { data } = await apiClient.put<SchedulingAlgorithmConfigDto>(`${BASE}/${id}`, { value });
  return data;
}
