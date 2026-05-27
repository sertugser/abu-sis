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

export interface PreviewAcademicCalendarEventMoveDto extends UpdateAcademicCalendarEventDto {
  events: TermCalendarEventDto[];
}

export interface SaveAcademicCalendarTermEventsDto {
  termId: number;
  events: TermCalendarEventDto[];
}

export interface SaveAcademicCalendarChangesDto {
  calendars: SaveAcademicCalendarTermEventsDto[];
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

export function useAcademicYearCalendars(termIds: number[], includeAllEvents = false) {
  return useQueries({
    queries: termIds.map(termId => ({
      queryKey: QUERY_KEYS.studentAcademicCalendar(termId, includeAllEvents),
      queryFn: async () => {
        const { data } = await apiClient.get<StudentAcademicCalendarDto>(`${PUBLIC}/${termId}`, {
          params: includeAllEvents ? { includeAllEvents: true } : undefined,
        });
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

export function usePreviewAcademicCalendarEventMove() {
  return useMutation({
    mutationFn: async ({ termId, eventId, dto }: { termId: number; eventId: number; dto: PreviewAcademicCalendarEventMoveDto }) => {
      const { data } = await apiClient.post<StudentAcademicCalendarDto>(`${PUBLIC}/${termId}/events/${eventId}/preview-move`, dto);
      return data;
    },
  });
}

export function useSaveAcademicCalendarChanges() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (dto: SaveAcademicCalendarChangesDto) => {
      const { data } = await apiClient.put<StudentAcademicCalendarDto[]>(`${PUBLIC}/events`, dto);
      return data;
    },
    onSuccess: async calendars => {
      await Promise.all([
        ...calendars.flatMap(calendar => [
          queryClient.invalidateQueries({ queryKey: QUERY_KEYS.studentAcademicCalendar(calendar.termId) }),
          queryClient.invalidateQueries({ queryKey: QUERY_KEYS.studentAcademicCalendar(calendar.termId, true) }),
        ]),
        queryClient.invalidateQueries({ queryKey: QUERY_KEYS.studentAcademicCalendarActive }),
      ]);

      for (const calendar of calendars)
        queryClient.setQueryData(QUERY_KEYS.studentAcademicCalendar(calendar.termId, true), calendar);
    },
  });
}
