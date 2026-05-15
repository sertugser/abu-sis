import apiClient from './axios';

export interface EffectiveRolesDto {
  ownRoles: string[];
  delegatedRoles: string[];
}

export const effectiveRolesApi = {
  async get(): Promise<EffectiveRolesDto> {
    const { data } = await apiClient.get<EffectiveRolesDto>('/me/effective-roles');
    return data;
  },
};
