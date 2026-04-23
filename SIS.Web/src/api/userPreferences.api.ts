import apiClient from './axios';

export interface UserPreferenceDto {
  favorites: string[];
  hasCustomized: boolean;
}

export async function getUserPreferences(signal?: AbortSignal): Promise<UserPreferenceDto> {
  const response = await apiClient.get<UserPreferenceDto>('/user/preferences', { signal });
  return response.data;
}

export async function saveUserPreferences(favorites: string[]): Promise<UserPreferenceDto> {
  const response = await apiClient.put<UserPreferenceDto>('/user/preferences', { favorites });
  return response.data;
}
