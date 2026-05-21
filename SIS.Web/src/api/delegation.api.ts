import apiClient from './axios';

export type DelegationStatus = 'Active' | 'Pending' | 'Expired' | 'Revoked';

export interface PermissionDelegationDto {
  id: number;
  delegatorUserId: number;
  delegatorUserName: string;
  delegatorFullName: string;
  delegateeUserId: number;
  delegateeUserName: string;
  delegateeFullName: string;
  startsAtUtc: string;
  endsAtUtc: string;
  reason: string;
  revokedAtUtc: string | null;
  revokedByUserId: number | null;
  revokeReason: string | null;
  createdAtUtc: string;
  createdByUserId: number;
  status: DelegationStatus;
}

export interface CreateDelegationDto {
  delegatorUserId: number;
  delegateeUserId: number;
  startsAtUtc: string; // ISO UTC
  endsAtUtc: string;
  reason: string;
}

export interface RevokeDelegationDto {
  reason?: string | null;
}

export interface ListDelegationsParams {
  includeInactive?: boolean;
  delegatorUserId?: number;
  delegateeUserId?: number;
}

export const delegationApi = {
  async list(params: ListDelegationsParams = {}): Promise<PermissionDelegationDto[]> {
    const { data } = await apiClient.get<PermissionDelegationDto[]>('/admin/delegations', {
      params: {
        includeInactive: params.includeInactive ?? false,
        delegatorUserId: params.delegatorUserId,
        delegateeUserId: params.delegateeUserId,
      },
    });
    return data;
  },

  async get(id: number): Promise<PermissionDelegationDto> {
    const { data } = await apiClient.get<PermissionDelegationDto>(`/admin/delegations/${id}`);
    return data;
  },

  async create(dto: CreateDelegationDto): Promise<PermissionDelegationDto> {
    const { data } = await apiClient.post<PermissionDelegationDto>('/admin/delegations', dto);
    return data;
  },

  async revoke(id: number, dto: RevokeDelegationDto): Promise<PermissionDelegationDto> {
    const { data } = await apiClient.post<PermissionDelegationDto>(
      `/admin/delegations/${id}/revoke`,
      dto,
    );
    return data;
  },
};
