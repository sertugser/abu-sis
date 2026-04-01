import apiClient from './axios';

export interface SystemConfigDto {
  id: number;
  key: string;
  value: string;
  description: string;
  category: string;
  dataType: string;
}

export async function getSystemConfigs(): Promise<SystemConfigDto[]> {
  const { data } = await apiClient.get<SystemConfigDto[]>('/admin/system-config');
  return data;
}

export async function updateSystemConfig(id: number, value: string): Promise<SystemConfigDto> {
  const { data } = await apiClient.put<SystemConfigDto>(`/admin/system-config/${id}`, { value });
  return data;
}
