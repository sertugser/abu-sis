import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { QUERY_KEYS } from '../lib/constants';
import apiClient from '../api/axios';

const API = '/admin/terms';

export interface AdminTermDto {
  id: number;
  name: string;
  shortName: string;
  englishName: string;
  year: number;
  semester: string;
  semesterLabel: string;
  termType: string;
  termTypeLabel: string;
  isActive: boolean;
  registrationStart: string;
  registrationEnd: string;
  addDropStart: string;
  addDropEnd: string;
  termStart: string;
  termEnd: string;
  withdrawalDeadline: string;
  isRegistrationOpen: boolean;
  isAddDropOpen: boolean;
  status: string;
  statusLabel: string;
  allowedTransitions: string[];
}

export interface CreateTermDto {
  name: string;
  shortName: string;
  englishName: string;
  year: number;
  semester: string;
  termType: string;
  registrationStart: string;
  registrationEnd: string;
  addDropStart: string;
  addDropEnd: string;
  termStart: string;
  termEnd: string;
  withdrawalDeadline: string;
}

export interface UpdateTermDto {
  name: string;
  shortName: string;
  englishName: string;
  termType: string;
  registrationStart: string;
  registrationEnd: string;
  addDropStart: string;
  addDropEnd: string;
  termStart: string;
  termEnd: string;
  withdrawalDeadline: string;
}


// ── Term CRUD ────────────────────────────────────────────────────────────────

export function useAdminTerms() {
  return useQuery({
    queryKey: QUERY_KEYS.adminTerms,
    queryFn: async () => {
      const { data } = await apiClient.get<AdminTermDto[]>(API);
      return data;
    },
  });
}
export function useCreateTerm() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dto: CreateTermDto) => {
      const { data } = await apiClient.post<AdminTermDto>(API, dto);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.adminTerms });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.activeTerm });
    },
  });
}

export function useUpdateTerm() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, dto }: { id: number; dto: UpdateTermDto }) => {
      const { data } = await apiClient.put<AdminTermDto>(`${API}/${id}`, dto);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.adminTerms });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.activeTerm });
    },
  });
}

export function useActivateTerm() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await apiClient.post(`${API}/${id}/activate`);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.adminTerms });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.activeTerm });
    },
  });
}

// ── Term Status Transition ───────────────────────────────────────────────────

export function useTransitionTermStatus() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ termId, targetStatus }: { termId: number; targetStatus: string }) => {
      await apiClient.post(`${API}/${termId}/transition`, { targetStatus });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.adminTerms });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.activeTerm });
    },
  });
}
