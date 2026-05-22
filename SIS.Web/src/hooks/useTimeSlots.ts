import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import apiClient from '../api/axios';

const BASE = '/admin/time-slots';

export type TimeSlotAllowedMode = 'Both' | 'OnlineOnly' | 'FaceToFaceOnly';

/**
 * Wire format: .NET System.DayOfWeek as int (Sunday=0, Monday=1, ..., Saturday=6).
 * Backend `TimeSlotDto.DayOfWeek` is declared as `int` and serialized as a number.
 * Name kept for ergonomics, but the underlying type is numeric.
 */
export type DayOfWeekName = 0 | 1 | 2 | 3 | 4 | 5 | 6;

export interface TimeSlotDto {
  id: number;
  termId: number;
  dayOfWeek: DayOfWeekName;
  startTime: string;
  endTime: string;
  slotOrder: number;
  displayName: string;
  allowedMode: TimeSlotAllowedMode;
}

export interface CreateTimeSlotDto {
  termId: number;
  dayOfWeek: DayOfWeekName;
  startTime: string;
  endTime: string;
  allowedMode: TimeSlotAllowedMode;
}

export interface UpdateTimeSlotDto {
  dayOfWeek: DayOfWeekName;
  startTime: string;
  endTime: string;
  allowedMode: TimeSlotAllowedMode;
}

export interface TimeSlotDeleteResultDto {
  id: number;
  dependentScheduleCount: number;
  deleted: boolean;
  message: string | null;
}

export interface RegenerateTimeSlotsResultDto {
  termId: number;
  generatedCount: number;
  dependentScheduleCount: number;
  regenerated: boolean;
  message: string | null;
}

const QK_BASE = ['admin', 'time-slots'] as const;
const qk = (termId: number) => [...QK_BASE, termId] as const;

export function useTimeSlots(termId: number | null) {
  return useQuery({
    queryKey: termId ? qk(termId) : [...QK_BASE, 'none'],
    queryFn: async () => {
      const { data } = await apiClient.get<TimeSlotDto[]>(BASE, { params: { termId } });
      return data;
    },
    enabled: !!termId,
    staleTime: 60 * 1000,
  });
}

export function useCreateTimeSlot() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dto: CreateTimeSlotDto) => {
      const { data } = await apiClient.post<TimeSlotDto>(BASE, dto);
      return data;
    },
    onSuccess: (slot) => qc.invalidateQueries({ queryKey: qk(slot.termId) }),
  });
}

export function useUpdateTimeSlot() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, dto }: { id: number; dto: UpdateTimeSlotDto }) => {
      const { data } = await apiClient.put<TimeSlotDto>(`${BASE}/${id}`, dto);
      return data;
    },
    onSuccess: (slot) => qc.invalidateQueries({ queryKey: qk(slot.termId) }),
  });
}

export function useDeleteTimeSlot() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, force, termId }: { id: number; force: boolean; termId: number }) => {
      try {
        const { data } = await apiClient.delete<TimeSlotDeleteResultDto>(
          `${BASE}/${id}`,
          { params: { force } }
        );
        return { result: data, termId };
      } catch (e: unknown) {
        const err = e as { response?: { status?: number; data?: TimeSlotDeleteResultDto } };
        if (err.response?.status === 409 && err.response.data) {
          return { result: err.response.data, termId };
        }
        throw e;
      }
    },
    onSuccess: ({ result, termId }) => {
      if (result.deleted) qc.invalidateQueries({ queryKey: qk(termId) });
    },
  });
}

export function useRegenerateTimeSlots() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ termId, force }: { termId: number; force: boolean }) => {
      try {
        const { data } = await apiClient.post<RegenerateTimeSlotsResultDto>(
          `${BASE}/regenerate`,
          null,
          { params: { termId, force } }
        );
        return data;
      } catch (e: unknown) {
        const err = e as { response?: { status?: number; data?: RegenerateTimeSlotsResultDto } };
        if (err.response?.status === 409 && err.response.data) return err.response.data;
        throw e;
      }
    },
    onSuccess: (result) => {
      if (result.regenerated) qc.invalidateQueries({ queryKey: qk(result.termId) });
    },
  });
}

export const ALLOWED_MODE_OPTIONS: { value: TimeSlotAllowedMode; label: string }[] = [
  { value: 'Both', label: 'Her ikisi (yüz-yüze + online)' },
  { value: 'OnlineOnly', label: 'Yalnızca online' },
  { value: 'FaceToFaceOnly', label: 'Yalnızca yüz-yüze' },
];

export const DAY_OF_WEEK_OPTIONS: { value: DayOfWeekName; label: string }[] = [
  { value: 1, label: 'Pazartesi' },
  { value: 2, label: 'Salı' },
  { value: 3, label: 'Çarşamba' },
  { value: 4, label: 'Perşembe' },
  { value: 5, label: 'Cuma' },
  { value: 6, label: 'Cumartesi' },
  { value: 0, label: 'Pazar' },
];

export function dayName(day: DayOfWeekName): string {
  return DAY_OF_WEEK_OPTIONS.find(d => d.value === day)?.label ?? String(day);
}

/** Hafta sonu kontrolü (Cts/Paz) — System.DayOfWeek: Sun=0, Sat=6 */
export function isWeekend(day: DayOfWeekName): boolean {
  return day === 0 || day === 6;
}

export function allowedModeLabel(mode: TimeSlotAllowedMode): string {
  return ALLOWED_MODE_OPTIONS.find(m => m.value === mode)?.label ?? mode;
}
