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

  // Status union narrowed to 5 values
  status: 'Draft' | 'Preparing' | 'Ready' | 'Closed' | 'Archived';
  statusLabel: string;
  allowedTransitions: string[];

  // Date fields
  registrationStart: string;
  registrationEnd: string;
  addDropStart: string;
  addDropEnd: string;
  termStart: string;
  termEnd: string;
  withdrawalDeadline: string;
  gradeEntryStart: string;
  gradeEntryEnd: string;

  // Override fields
  registrationOverride: boolean | null;
  addDropOverride: boolean | null;
  withdrawalOverride: boolean | null;
  gradeEntryOverride: boolean | null;

  // Computed phase flags
  isRegistrationOpen: boolean;
  isAddDropOpen: boolean;
  isWithdrawalOpen: boolean;
  isGradeEntryOpen: boolean;
  isActive: boolean;

  // Calendar fields
  calendarEventCount: number;
  generatedCalendarEventCount: number;
  generatedCalendarDraftId: number | null;
  hasGeneratedCalendar: boolean;
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
  gradeEntryStart: string;
  gradeEntryEnd: string;
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
  gradeEntryStart: string;
  gradeEntryEnd: string;
}

export interface UpdateTermPhaseOverrideDto {
  phase: 'Registration' | 'AddDrop' | 'Withdrawal' | 'GradeEntry';
  value: boolean | null;
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
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.adminTerms });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.activeTerm });
    },
  });
}
