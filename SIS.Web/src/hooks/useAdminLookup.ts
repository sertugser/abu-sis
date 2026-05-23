import { useQuery } from '@tanstack/react-query';
import apiClient from '../api/axios';

export interface LookupFaculty {
  id: number;
  name: string;
  code: string;
}

export interface LookupDepartment {
  id: number;
  name: string;
  code: string;
}

export interface LookupProgram {
  id: number;
  name: string;
  code: string;
  departmentId: number;
}

export interface LookupAdvisor {
  id: number;
  fullName: string;
  departmentName: string;
}

/**
 * Fakülte lookup'ı — dropdown/filtre için hafif {id,name,code} listesi.
 * Backend `lookup.faculty` (her authenticated user'a açık, scope-filtreli) ucunu çağırır.
 * Fakülte YÖNETİM sayfaları (count alanları gerektiren) için `useAdminFaculties` kullanılmalı.
 */
export function useFacultyLookup() {
  return useQuery({
    queryKey: ['admin', 'lookup', 'faculties'],
    queryFn: async () => {
      const { data } = await apiClient.get<LookupFaculty[]>('/admin/faculties');
      return data;
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useAdminDepartments() {
  return useQuery({
    queryKey: ['admin', 'lookup', 'departments'],
    queryFn: async () => {
      const { data } = await apiClient.get<LookupDepartment[]>('/admin/departments');
      return data;
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useAdminPrograms(departmentId?: number) {
  return useQuery({
    queryKey: ['admin', 'lookup', 'programs', departmentId],
    queryFn: async () => {
      const { data } = await apiClient.get<LookupProgram[]>('/admin/programs', {
        params: departmentId ? { departmentId } : undefined,
      });
      return data;
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useAdminAdvisors() {
  return useQuery({
    queryKey: ['admin', 'lookup', 'advisors'],
    queryFn: async () => {
      const { data } = await apiClient.get<LookupAdvisor[]>('/admin/advisors');
      return data;
    },
    staleTime: 5 * 60 * 1000,
  });
}
