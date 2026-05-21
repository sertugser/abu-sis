import apiClient from './axios';

export type ScopeKind = 'Faculty';

export interface UserScopeDto {
  userId: number;
  kind: ScopeKind;
  facultyId: number | null;
  createdAt: string;
  updatedAt: string;
}

export interface SetUserScopeDto {
  kind: ScopeKind;
  facultyId: number | null;
}

export const userScopeApi = {
  async get(userId: number): Promise<UserScopeDto | null> {
    try {
      const { data } = await apiClient.get<UserScopeDto>(`/admin/users/${userId}/scope`);
      return data;
    } catch (error: unknown) {
      const status = typeof error === 'object' && error !== null
        ? (error as { response?: { status?: number } }).response?.status
        : undefined;
      if (status === 404) {
        return null;
      }

      throw error;
    }
  },

  async set(userId: number, dto: SetUserScopeDto): Promise<UserScopeDto> {
    const { data } = await apiClient.put<UserScopeDto>(`/admin/users/${userId}/scope`, dto);
    return data;
  },

  async clear(userId: number): Promise<void> {
    await apiClient.delete(`/admin/users/${userId}/scope`);
  },
};
