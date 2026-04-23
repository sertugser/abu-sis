import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import apiClient from '../api/axios';

const BASE = '/admin/osym-registration-calendars';

export type OsymRegistrationTypeValue =
  | 'YksPlacement' | 'HorizontalTransfer' | 'VerticalTransfer' | 'ForeignStudent';

export interface OsymRegistrationCalendarDto {
  id: number;
  year: number;
  type: OsymRegistrationTypeValue;
  typeLabel: string;
  name: string;
  startDate: string;
  endDate: string;
  description: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateOsymRegistrationCalendarDto {
  year: number;
  type: OsymRegistrationTypeValue;
  name: string;
  startDate: string;
  endDate: string;
  description: string | null;
  isActive: boolean;
}

export type UpdateOsymRegistrationCalendarDto = CreateOsymRegistrationCalendarDto;

const QK = ['admin', 'osym-registration-calendars'] as const;

export function useOsymRegistrationCalendars(year?: number) {
  return useQuery({
    queryKey: year ? [...QK, year] : QK,
    queryFn: async () => {
      const { data } = await apiClient.get<OsymRegistrationCalendarDto[]>(
        BASE, year ? { params: { year } } : undefined);
      return data;
    },
    staleTime: 2 * 60 * 1000,
  });
}

export function useCreateOsymRegistrationCalendar() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dto: CreateOsymRegistrationCalendarDto) => {
      const { data } = await apiClient.post<OsymRegistrationCalendarDto>(BASE, dto);
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: QK }),
  });
}

export function useUpdateOsymRegistrationCalendar() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...dto }: { id: number } & UpdateOsymRegistrationCalendarDto) => {
      const { data } = await apiClient.put<OsymRegistrationCalendarDto>(`${BASE}/${id}`, dto);
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: QK }),
  });
}

export function useDeleteOsymRegistrationCalendar() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => { await apiClient.delete(`${BASE}/${id}`); },
    onSuccess: () => qc.invalidateQueries({ queryKey: QK }),
  });
}

export const OSYM_TYPE_OPTIONS: { value: OsymRegistrationTypeValue; label: string }[] = [
  { value: 'YksPlacement', label: 'YKS Yerleştirme' },
  { value: 'HorizontalTransfer', label: 'Yatay Geçiş' },
  { value: 'VerticalTransfer', label: 'Dikey Geçiş' },
  { value: 'ForeignStudent', label: 'Yabancı Uyruklu Öğrenci' },
];
