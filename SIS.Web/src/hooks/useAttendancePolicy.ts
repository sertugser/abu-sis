import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import apiClient from '../api/axios';

const API = '/admin/attendance-policy';

export type AttendanceMethodValue = 'Separate' | 'TotalHours' | 'Passive' | 'MarkOnly';

export interface TermAttendancePolicyDto {
  programId: number | null;
  programName: string | null;
  termWeekCount: number;
  theoryAbsenceLimitPercent: number;
  labAbsenceLimitPercent: number;
  method: AttendanceMethodValue;
}

export interface TermAttendancePolicyListDto {
  termId: number;
  termName: string;
  globalDefault: TermAttendancePolicyDto;
  policies: TermAttendancePolicyDto[];
}

export interface UpsertTermAttendancePolicyRequest {
  programId: number | null;
  termWeekCount: number;
  theoryAbsenceLimitPercent: number;
  labAbsenceLimitPercent: number;
  method: AttendanceMethodValue;
}

export interface ProgramLookupItem {
  id: number;
  name: string;
  code: string;
  departmentId: number | null;
}

export function useTermAttendancePolicy(termId: number | null) {
  return useQuery({
    queryKey: ['admin', 'attendance-policy', termId],
    queryFn: async () => {
      const { data } = await apiClient.get<TermAttendancePolicyListDto>(`${API}/${termId}`);
      return data;
    },
    enabled: termId != null,
  });
}

export function useProgramLookup() {
  return useQuery({
    queryKey: ['admin', 'lookup', 'programs'],
    queryFn: async () => {
      const { data } = await apiClient.get<ProgramLookupItem[]>('/admin/lookup/programs');
      return data;
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useUpsertTermAttendancePolicy(termId: number | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: UpsertTermAttendancePolicyRequest) => {
      const { data } = await apiClient.put<TermAttendancePolicyListDto>(`${API}/${termId}`, req);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['admin', 'attendance-policy', termId] });
    },
  });
}

export function useDeleteProgramOverride(termId: number | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (programId: number) => {
      const { data } = await apiClient.delete<TermAttendancePolicyListDto>(`${API}/${termId}/program/${programId}`);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['admin', 'attendance-policy', termId] });
    },
  });
}
