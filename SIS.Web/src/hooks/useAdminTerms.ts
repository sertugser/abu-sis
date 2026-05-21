import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { QUERY_KEYS } from '../lib/constants';
import apiClient from '../api/axios';

const API = '/admin/terms';

export type TermTypeValue =
  | 'Fall'
  | 'Spring'
  | 'Summer'
  | 'FallResit'
  | 'SpringResit'
  | 'SummerResit'
  | 'SpringOther'
  | 'SummerOther'
  | 'Exemption';

export type TermStatusValue = 'Inactive' | 'Active' | 'Cancelled';
export type YoksisStatusValue = 'Inactive' | 'Active';

export interface AdminTermDto {
  id: number;
  name: string;
  shortName: string;
  englishName: string;
  shortEnglishName: string;
  otherName: string | null;
  year: number;

  termType: TermTypeValue;
  termTypeLabel: string;

  status: TermStatusValue;
  statusLabel: string;
  isActivated: boolean;
  isCancelled: boolean;

  yoksisStatus: YoksisStatusValue;

  termStart: string;
  termEnd: string;

  overrideStart: string | null;
  overrideEnd: string | null;
  effectiveStart: string;
  effectiveEnd: string;

  isActive: boolean;
  hasAcademicCalendar: boolean;
}

export interface CreateTermDto {
  name: string;
  shortName: string;
  englishName: string;
  shortEnglishName: string;
  otherName: string | null;
  year: number;
  termType: TermTypeValue;
  yoksisStatus: YoksisStatusValue;
  termStart: string;
  termEnd: string;
  overrideStart: string | null;
  overrideEnd: string | null;
}

export interface UpdateTermDto {
  name: string;
  shortName: string;
  englishName: string;
  shortEnglishName: string;
  otherName: string | null;
  termType: TermTypeValue;
  yoksisStatus: YoksisStatusValue;
  termStart: string;
  termEnd: string;
  overrideStart: string | null;
  overrideEnd: string | null;
}

function invalidateTerms(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: QUERY_KEYS.adminTerms });
  qc.invalidateQueries({ queryKey: QUERY_KEYS.activeTerm });
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
    onSuccess: () => invalidateTerms(qc),
  });
}

export function useUpdateTerm() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, dto }: { id: number; dto: UpdateTermDto }) => {
      const { data } = await apiClient.put<AdminTermDto>(`${API}/${id}`, dto);
      return data;
    },
    onSuccess: () => invalidateTerms(qc),
  });
}

// ── Term Cancel / Uncancel ───────────────────────────────────────────────────

export function useCancelTerm() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (termId: number) => {
      const { data } = await apiClient.post<AdminTermDto>(`${API}/${termId}/cancel`);
      return data;
    },
    onSuccess: () => invalidateTerms(qc),
  });
}

export function useUncancelTerm() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (termId: number) => {
      const { data } = await apiClient.post<AdminTermDto>(`${API}/${termId}/uncancel`);
      return data;
    },
    onSuccess: () => invalidateTerms(qc),
  });
}

export function useActivateTerm() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (termId: number) => {
      const { data } = await apiClient.post<AdminTermDto>(`${API}/${termId}/activate`);
      return data;
    },
    onSuccess: () => invalidateTerms(qc),
  });
}

export function useDeactivateTerm() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (termId: number) => {
      const { data } = await apiClient.post<AdminTermDto>(`${API}/${termId}/deactivate`);
      return data;
    },
    onSuccess: () => invalidateTerms(qc),
  });
}
