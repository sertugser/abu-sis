import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { QUERY_KEYS } from '../lib/constants';
import apiClient from '../api/axios';

const API = '/admin/users';

export interface AdminUserListItem {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  roles: string[];
  isActive: boolean;
  userType: 'Student' | 'Instructor' | 'Admin' | 'Other';
}

export interface AdminUserDetail extends AdminUserListItem {
  studentNumber?: string;
  instructorTitle?: string;
  department?: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface AdminUserFilter {
  search?: string;
  role?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
}

export function useAdminUsers(filter: AdminUserFilter) {
  return useQuery({
    queryKey: QUERY_KEYS.adminUsers(filter.search, filter.role, filter.isActive, filter.page),
    queryFn: async () => {
      const { data } = await apiClient.get<PagedResult<AdminUserListItem>>(API, { params: filter });
      return data;
    },
  });
}

export function useAdminUser(id: number | null) {
  return useQuery({
    queryKey: ['admin', 'users', id],
    queryFn: async () => {
      const { data } = await apiClient.get<AdminUserDetail>(`${API}/${id}`);
      return data;
    },
    enabled: id !== null,
  });
}

export function useUpdateUserRoles() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, roles }: { id: number; roles: string[] }) => {
      await apiClient.put(`${API}/${id}/roles`, { roles });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'users'] }),
  });
}

export function useResetPassword() {
  return useMutation({
    mutationFn: async ({ id, newPassword }: { id: number; newPassword: string }) => {
      await apiClient.post(`${API}/${id}/reset-password`, { newPassword });
    },
  });
}

export function useSetUserStatus() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, isActive }: { id: number; isActive: boolean }) => {
      await apiClient.put(`${API}/${id}/status`, { isActive });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'users'] }),
  });
}
