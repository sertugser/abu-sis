import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { QUERY_KEYS } from '../lib/constants';
import apiClient from '../api/axios';

const ADMIN = '/admin/academic-calendar';
const PUBLIC = '/academic-calendar';

// ── Types ────────────────────────────────────────────────────────

export interface AcademicCalendarDto {
  id: number;
  termId: number;
  termName: string;
  termStart: string;
  termEnd: string;
  registrationStart: string;
  registrationEnd: string;
  addDropStart: string;
  addDropEnd: string;
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
  createdAt: string;
  updatedAt: string | null;
}

export interface UpsertAcademicCalendarDto {
  registrationStart: string;
  registrationEnd: string;
  addDropStart: string;
  addDropEnd: string;
  withdrawalDeadline: string;
  gradeEntryStart: string;
  gradeEntryEnd: string;
}

export type PhaseKey = 'Registration' | 'AddDrop' | 'Withdrawal' | 'GradeEntry';

export interface UpdateCalendarPhaseOverrideDto {
  phase: PhaseKey;
  value: boolean | null;
}

export interface AcademicCalendarTermSummaryDto {
  termId: number;
  termName: string;
  year: number;
  termTypeLabel: string;
  isActive: boolean;
  isCancelled: boolean;
  hasCalendar: boolean;
  termStart: string;
  termEnd: string;
}

export interface TermCalendarEventDto {
  id: number;
  eventType: string;
  title: string;
  startDate: string;
  endDate: string | null;
  note: string | null;
  isPublic: boolean;
  isGenerated: boolean;
  blocksAcademicEvents: boolean;
}

export interface StudentAcademicCalendarDto {
  termId: number;
  termName: string;
  termTypeLabel: string;
  year: number;
  isCancelled: boolean;
  termStart: string;
  termEnd: string;
  registrationStart: string | null;
  registrationEnd: string | null;
  addDropStart: string | null;
  addDropEnd: string | null;
  withdrawalDeadline: string | null;
  gradeEntryStart: string | null;
  gradeEntryEnd: string | null;
  isRegistrationOpen: boolean;
  isAddDropOpen: boolean;
  isWithdrawalOpen: boolean;
  isGradeEntryOpen: boolean;
  events: TermCalendarEventDto[];
}

// ── Admin hooks ─────────────────────────────────────────────────

export function useAdminCalendarTerms() {
  return useQuery({
    queryKey: QUERY_KEYS.adminAcademicCalendarTerms,
    queryFn: async () => {
      const { data } = await apiClient.get<AcademicCalendarTermSummaryDto[]>(ADMIN);
      return data;
    }
  });
}

export function useAdminAcademicCalendar(termId: number | undefined) {
  return useQuery({
    queryKey: QUERY_KEYS.adminAcademicCalendar(termId),
    queryFn: async () => {
      const { data } = await apiClient.get<AcademicCalendarDto>(`${ADMIN}/${termId}`);
      return data;
    },
    enabled: !!termId,
    retry: false
  });
}

export function useUpsertAcademicCalendar(termId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dto: UpsertAcademicCalendarDto) => {
      const { data } = await apiClient.put<AcademicCalendarDto>(`${ADMIN}/${termId}`, dto);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.adminAcademicCalendar(termId) });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.adminAcademicCalendarTerms });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.activeTerm });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentAcademicCalendar(termId) });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentAcademicCalendarActive });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.academicCalendarTerms });
    }
  });
}

export function useUpdateCalendarPhaseOverride(termId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dto: UpdateCalendarPhaseOverrideDto) => {
      const { data } = await apiClient.post<AcademicCalendarDto>(
        `${ADMIN}/${termId}/override`, dto);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.adminAcademicCalendar(termId) });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.activeTerm });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentAcademicCalendar(termId) });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.studentAcademicCalendarActive });
    }
  });
}

// ── Public hooks ─────────────────────────────────────────────────

export function useActiveStudentCalendar() {
  return useQuery({
    queryKey: QUERY_KEYS.studentAcademicCalendarActive,
    queryFn: async () => {
      const { data } = await apiClient.get<StudentAcademicCalendarDto>(`${PUBLIC}/active`);
      return data;
    },
    retry: false
  });
}

export function useStudentCalendar(termId: number | null | undefined) {
  return useQuery({
    queryKey: QUERY_KEYS.studentAcademicCalendar(termId ?? null),
    queryFn: async () => {
      const { data } = await apiClient.get<StudentAcademicCalendarDto>(`${PUBLIC}/${termId}`);
      return data;
    },
    enabled: !!termId
  });
}

export function useAcademicCalendarTerms() {
  return useQuery({
    queryKey: QUERY_KEYS.academicCalendarTerms,
    queryFn: async () => {
      const { data } = await apiClient.get<AcademicCalendarTermSummaryDto[]>(`${PUBLIC}/terms`);
      return data;
    }
  });
}
