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
  isCancelled: boolean;

  yoksisStatus: YoksisStatusValue;

  registrationStart: string;
  registrationEnd: string;
  addDropStart: string;
  addDropEnd: string;
  termStart: string;
  termEnd: string;
  withdrawalDeadline: string;
  gradeEntryStart: string;
  gradeEntryEnd: string;

  registrationOverride: boolean | null;
  addDropOverride: boolean | null;
  withdrawalOverride: boolean | null;
  gradeEntryOverride: boolean | null;

  isRegistrationOpen: boolean;
  isAddDropOpen: boolean;
  isWithdrawalOpen: boolean;
  isGradeEntryOpen: boolean;
  isActive: boolean;

  calendarEventCount: number;
  generatedCalendarEventCount: number;
  generatedCalendarDraftId: number | null;
  hasGeneratedCalendar: boolean;
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
  registrationStart: string;
  registrationEnd: string;
  addDropStart: string;
  addDropEnd: string;
  termStart: string;
  termEnd: string;
  withdrawalDeadline: string;
  gradeEntryStart: string;
  gradeEntryEnd: string;
}

export interface UpdateTermDto {
  name: string;
  shortName: string;
  englishName: string;
  shortEnglishName: string;
  otherName: string | null;
  termType: TermTypeValue;
  yoksisStatus: YoksisStatusValue;
  registrationStart: string;
  registrationEnd: string;
  addDropStart: string;
  addDropEnd: string;
  termStart: string;
  termEnd: string;
  withdrawalDeadline: string;
  gradeEntryStart: string;
  gradeEntryEnd: string;
}

export interface UpdateTermPhaseOverrideDto {
  phase: 'Registration' | 'AddDrop' | 'Withdrawal' | 'GradeEntry';
  value: boolean | null;
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

export function useUpdateTermPhaseOverride() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      termId,
      phase,
      value,
    }: {
      termId: number;
      phase: UpdateTermPhaseOverrideDto['phase'];
      value: boolean | null;
    }) => {
      const { data } = await apiClient.post<AdminTermDto>(`${API}/${termId}/override`, {
        phase,
        value,
      });
      return data;
    },
    onSuccess: () => invalidateTerms(qc),
  });
}
