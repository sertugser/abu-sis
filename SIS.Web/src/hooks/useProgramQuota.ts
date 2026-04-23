import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import apiClient from '../api/axios';

const BASE = '/admin/program-quotas';

export type QuotaTypeValue = 'Yks' | 'LateralTransfer' | 'Postgraduate' | 'ForeignStudent';

export interface ProgramQuotaDto {
  id: number;
  programId: number;
  programName: string;
  departmentName: string;
  facultyName: string;
  year: number;
  type: QuotaTypeValue;
  typeLabel: string;
  quantity: number;
  description: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateProgramQuotaDto {
  programId: number;
  year: number;
  type: QuotaTypeValue;
  quantity: number;
  description: string | null;
}

export interface UpdateProgramQuotaDto {
  year: number;
  type: QuotaTypeValue;
  quantity: number;
  description: string | null;
}

const QK = ['admin', 'program-quotas'] as const;

export function useProgramQuotas(params: { programId?: number; year?: number } = {}) {
  return useQuery({
    queryKey: [...QK, params.programId ?? null, params.year ?? null],
    queryFn: async () => {
      const { data } = await apiClient.get<ProgramQuotaDto[]>(BASE, { params });
      return data;
    },
    staleTime: 2 * 60 * 1000,
  });
}

export function useCreateProgramQuota() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dto: CreateProgramQuotaDto) => {
      const { data } = await apiClient.post<ProgramQuotaDto>(BASE, dto);
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: QK }),
  });
}

export function useUpdateProgramQuota() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...dto }: { id: number } & UpdateProgramQuotaDto) => {
      const { data } = await apiClient.put<ProgramQuotaDto>(`${BASE}/${id}`, dto);
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: QK }),
  });
}

export function useDeleteProgramQuota() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => { await apiClient.delete(`${BASE}/${id}`); },
    onSuccess: () => qc.invalidateQueries({ queryKey: QK }),
  });
}

export const QUOTA_TYPE_OPTIONS: { value: QuotaTypeValue; label: string }[] = [
  { value: 'Yks', label: 'YKS' },
  { value: 'LateralTransfer', label: 'Yatay Geçiş' },
  { value: 'Postgraduate', label: 'Lisansüstü' },
  { value: 'ForeignStudent', label: 'Yabancı Uyruklu' },
];
