import apiClient from './axios';

/**
 * Kullanıcının tüm permission key'lerini döner.
 * Backend: GET /api/me/permissions
 *
 * v2 Karar 6: JWT'de permission claim YOK; bu endpoint frontend'de 5 dk cache'lenir.
 */
export async function getMyPermissions(): Promise<string[]> {
  const response = await apiClient.get<string[]>('/me/permissions');
  return response.data;
}
