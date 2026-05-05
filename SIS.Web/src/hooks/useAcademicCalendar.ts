import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { QUERY_KEYS } from '../lib/constants';
import apiClient from '../api/axios';

const PUBLIC = '/academic-calendar';

// ── Types ────────────────────────────────────────────────────────

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
  chainCode: string | null;
  templateItemKey: string | null;
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

export interface UpdateAcademicCalendarEventDto {
  startDate: string;
  endDate: string | null;
  propagateChanges?: boolean;
}

// ── Admin hooks ─────────────────────────────────────────────────

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

export function useAcademicYearCalendars(termIds: number[]) {
  return useQueries({
    queries: termIds.map(termId => ({
      queryKey: QUERY_KEYS.studentAcademicCalendar(termId),
      queryFn: async () => {
        const { data } = await apiClient.get<StudentAcademicCalendarDto>(`${PUBLIC}/${termId}`);
        return data;
      },
    })),
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

export function useMoveAcademicCalendarEvent() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ termId, eventId, dto }: { termId: number; eventId: number; dto: UpdateAcademicCalendarEventDto }) => {
      const { data } = await apiClient.put<StudentAcademicCalendarDto>(`${PUBLIC}/${termId}/events/${eventId}`, dto);
      return data;
    },
    onSuccess: async (calendar, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: QUERY_KEYS.studentAcademicCalendar(variables.termId) }),
        queryClient.invalidateQueries({ queryKey: QUERY_KEYS.studentAcademicCalendarActive }),
      ]);

      queryClient.setQueryData(QUERY_KEYS.studentAcademicCalendar(variables.termId), calendar);
    },
  });
}
