import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { QUERY_KEYS } from '../lib/constants';
import apiClient from '../api/axios';

const API = '/admin/instructors';

export interface AdminInstructorListItem {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  title: string;
  departmentName: string;
  departmentCode: string;
  isActive: boolean;
}

export interface AdminInstructorDetail extends AdminInstructorListItem {
  maxWeeklyHours: number;
  roles: string[];
}

export interface AdminInstructorFilter {
  search?: string;
  departmentId?: number;
  title?: string;
  isActive?: boolean;
}

export interface CreateInstructorDto {
  firstName: string;
  lastName: string;
  title: string;
  departmentId: number;
  maxWeeklyHours: number;
  password: string;
  overrideEmail?: string;
}

export function useAdminInstructors(filter: AdminInstructorFilter) {
  return useQuery({
    queryKey: QUERY_KEYS.adminInstructors(filter.search, filter.departmentId),
    queryFn: async () => {
      const { data } = await apiClient.get<AdminInstructorListItem[]>(API, { params: filter });
      return data;
    },
  });
}

export function useCreateInstructor() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dto: CreateInstructorDto) => {
      const { data } = await apiClient.post<AdminInstructorDetail>(API, dto);
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'instructors'] }),
  });
}
