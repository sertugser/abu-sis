import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import apiClient from '../api/axios';

const API = '/admin/calendar-generator';

export type SemesterType = 'Fall' | 'Spring' | 'Summer';
export type CalendarAnchorType = 'AcademicYearStart' | 'TermStart' | 'PreviousItem' | 'FixedDate';
export type CalendarConstraintType = 'Pin' | 'Prohibition' | 'Duration' | 'MinimumTeachingWeeks' | 'Buffer' | 'Preference';
export type HolidayType = 'Fixed' | 'HijriCalculated' | 'ImportantDay';

export interface CalendarTemplateItemDto {
  key: string;
  eventType: string;
  title: string;
  defaultDurationDays: number;
  anchorType: CalendarAnchorType;
  anchorKey: string | null;
  offsetDays: number;
  applicableSemesters: SemesterType[];
  isRequired: boolean;
  sortOrder: number;
  avoidHolidayConflict: boolean;
}

export interface CalendarTemplateDto {
  id: number;
  name: string;
  chainCode: string;
  isActive: boolean;
  items: CalendarTemplateItemDto[];
}

export type UpsertCalendarTemplateDto = Omit<CalendarTemplateDto, 'id'>;

export interface HolidayOverrideDto {
  year: number;
  startDate: string;
  endDate: string;
  isEstimated: boolean;
  source: string | null;
}

export interface HolidayDefinitionDto {
  id: number;
  name: string;
  englishName: string;
  type: HolidayType;
  fixedMonth: number | null;
  fixedDay: number | null;
  hijriMonth: number | null;
  hijriDay: number | null;
  durationDays: number;
  isHalfDayStart: boolean;
  isOfficialHoliday: boolean;
  blocksAcademicEvents: boolean;
  source: string;
  overrides: HolidayOverrideDto[];
  isActive: boolean;
}

export interface CalendarConstraintInputDto {
  constraintType: CalendarConstraintType;
  targetEventType: string | null;
  targetChainCode: string | null;
  parameterJson: string;
  isHard: boolean;
  priority: number;
  description: string | null;
}

export interface ResolvedHolidayDto {
  name: string;
  englishName: string;
  type: HolidayType;
  startDate: string;
  endDate: string;
  durationDays: number;
  isOfficialHoliday: boolean;
  blocksAcademicEvents: boolean;
  isEstimated: boolean;
  source: string;
}

export interface GeneratedCalendarEventDto {
  draftEventId: string;
  semester: SemesterType;
  termKey: string;
  eventType: string;
  eventTypeLabel: string;
  title: string;
  startDate: string;
  endDate: string | null;
  chainCode: string;
  templateItemKey: string | null;
  isPublic: boolean;
  isHoliday: boolean;
  blocksAcademicEvents: boolean;
  isFailed: boolean;
  note: string | null;
}

export interface CalendarWarningDto {
  code: string;
  severity: string;
  message: string;
  draftEventId: string | null;
}

export interface TermSummaryDto {
  semester: SemesterType;
  termStart: string;
  termEnd: string;
  teachingDays: number;
  holidayDaysInTerm: number;
  examDays: number;
}

export interface GenerateCalendarRequestDto {
  academicYear: string;
  fallStartDate: string | null;
  springStartDate: string | null;
  includeSummer: boolean;
  constraints: CalendarConstraintInputDto[] | null;
  templateDurationOverrides: Record<string, number> | null;
}

export interface GenerateCalendarResultDto {
  draftId: number;
  academicYear: string;
  qualityScore: number;
  events: GeneratedCalendarEventDto[];
  holidays: ResolvedHolidayDto[];
  warnings: CalendarWarningDto[];
  termSummaries: TermSummaryDto[];
}

export interface CalendarDraftDto {
  id: number;
  academicYear: string;
  status: string;
  qualityScore: number;
  createdAt: string;
  updatedAt: string;
  expiresAt: string;
  events: GeneratedCalendarEventDto[];
  holidays: ResolvedHolidayDto[];
  warnings: CalendarWarningDto[];
  termSummaries: TermSummaryDto[];
}

export interface CalendarDraftSummaryDto {
  id: number;
  academicYear: string;
  status: string;
  qualityScore: number;
  createdAt: string;
  updatedAt: string;
  expiresAt: string;
  appliedAt: string | null;
  eventCount: number;
  warningCount: number;
}

export interface UpdateDraftEventDto {
  startDate: string;
  endDate: string | null;
  title: string | null;
  note: string | null;
  propagateChanges?: boolean | null;
}

export interface WhatIfRequestDto {
  draftEventId: string;
  startDate: string;
  endDate: string | null;
  propagateChanges?: boolean | null;
}

export interface WhatIfResultDto {
  events: GeneratedCalendarEventDto[];
  warnings: CalendarWarningDto[];
  qualityScore: number;
}

export interface ApplyCalendarDraftResultDto {
  success: boolean;
  message: string;
  termIds: number[];
}

export function useCalendarTemplates() {
  return useQuery({
    queryKey: ['calendar-generator', 'templates'],
    queryFn: async () => {
      const { data } = await apiClient.get<CalendarTemplateDto[]>(`${API}/templates`);
      return data;
    },
  });
}

export function useSaveCalendarTemplates() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (templates: UpsertCalendarTemplateDto[]) => {
      const { data } = await apiClient.put<CalendarTemplateDto[]>(`${API}/templates`, templates);
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['calendar-generator', 'templates'] }),
  });
}

export function useHolidayDefinitions() {
  return useQuery({
    queryKey: ['calendar-generator', 'holidays'],
    queryFn: async () => {
      const { data } = await apiClient.get<HolidayDefinitionDto[]>(`${API}/holidays/definitions`);
      return data;
    },
  });
}

export function useGenerateCalendar() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (request: GenerateCalendarRequestDto) => {
      const { data } = await apiClient.post<GenerateCalendarResultDto>(`${API}/generate`, request);
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['calendar-generator', 'drafts'] }),
  });
}

export function useCalendarDrafts(enabled = true) {
  return useQuery({
    queryKey: ['calendar-generator', 'drafts'],
    queryFn: async () => {
      const { data } = await apiClient.get<CalendarDraftSummaryDto[]>(`${API}/drafts`, { params: { take: 100 } });
      return data;
    },
    enabled,
  });
}

export function useCalendarDraft(draftId: number | null) {
  return useQuery({
    queryKey: ['calendar-generator', 'draft', draftId],
    queryFn: async () => {
      const { data } = await apiClient.get<CalendarDraftDto>(`${API}/drafts/${draftId}`);
      return data;
    },
    enabled: draftId !== null,
  });
}

export function useUpdateDraftEvent() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ draftId, eventId, dto }: { draftId: number; eventId: string; dto: UpdateDraftEventDto }) => {
      const { data } = await apiClient.put<CalendarDraftDto>(`${API}/drafts/${draftId}/events/${encodeURIComponent(eventId)}`, dto);
      return data;
    },
    onSuccess: (_, variables) => {
      qc.invalidateQueries({ queryKey: ['calendar-generator', 'draft', variables.draftId] });
    },
  });
}

export function useWhatIfCalendar() {
  return useMutation({
    mutationFn: async ({ draftId, dto }: { draftId: number; dto: WhatIfRequestDto }) => {
      const { data } = await apiClient.post<WhatIfResultDto>(`${API}/drafts/${draftId}/what-if`, dto);
      return data;
    },
  });
}

export function useApplyDraft() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ draftId, forceOverwrite }: { draftId: number; forceOverwrite: boolean }) => {
      const { data } = await apiClient.post<ApplyCalendarDraftResultDto>(`${API}/drafts/${draftId}/apply`, { forceOverwrite });
      return data;
    },
    onSuccess: async (_, variables) => {
      await Promise.all([
        qc.invalidateQueries({ queryKey: ['calendar-generator', 'draft', variables.draftId] }),
        qc.invalidateQueries({ queryKey: ['calendar-generator', 'drafts'] }),
        qc.invalidateQueries({ queryKey: ['admin', 'terms'] }),
        qc.invalidateQueries({ queryKey: ['activeTerm'] }),
        qc.invalidateQueries({ queryKey: ['academic-calendar', 'active'] }),
      ]);
      await Promise.all([
        qc.refetchQueries({ queryKey: ['admin', 'terms'], type: 'active' }),
        qc.refetchQueries({ queryKey: ['activeTerm'], type: 'active' }),
        qc.refetchQueries({ queryKey: ['academic-calendar', 'active'], type: 'active' }),
      ]);
    },
  });
}

export function useRollbackDraft() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (draftId: number) => {
      const { data } = await apiClient.post<ApplyCalendarDraftResultDto>(`${API}/drafts/${draftId}/rollback`);
      return data;
    },
    onSuccess: async (_, draftId) => {
      await Promise.all([
        qc.invalidateQueries({ queryKey: ['calendar-generator', 'draft', draftId] }),
        qc.invalidateQueries({ queryKey: ['calendar-generator', 'drafts'] }),
        qc.invalidateQueries({ queryKey: ['admin', 'terms'] }),
        qc.invalidateQueries({ queryKey: ['activeTerm'] }),
        qc.invalidateQueries({ queryKey: ['academic-calendar', 'active'] }),
      ]);
      await Promise.all([
        qc.refetchQueries({ queryKey: ['admin', 'terms'], type: 'active' }),
        qc.refetchQueries({ queryKey: ['activeTerm'], type: 'active' }),
        qc.refetchQueries({ queryKey: ['academic-calendar', 'active'], type: 'active' }),
      ]);
    },
  });
}

export function useDeleteCalendarDraft() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (draftId: number) => {
      await apiClient.delete(`${API}/drafts/${draftId}`);
    },
    onSuccess: (_, draftId) => {
      qc.invalidateQueries({ queryKey: ['calendar-generator', 'drafts'] });
      qc.removeQueries({ queryKey: ['calendar-generator', 'draft', draftId] });
    },
  });
}
