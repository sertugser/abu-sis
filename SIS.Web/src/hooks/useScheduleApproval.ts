import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getScheduleApprovalStatus,
  approveSchedule,
  revokeScheduleApproval,
} from '../api/scheduleApproval.api';
import { QUERY_KEYS } from '../lib/constants';

export function useScheduleApprovalStatus(facultyId: number | null, termId?: number) {
  return useQuery({
    queryKey: QUERY_KEYS.scheduleApprovalStatus(facultyId ?? 0, termId),
    queryFn: () => getScheduleApprovalStatus(facultyId!, termId),
    enabled: facultyId != null,
    staleTime: 30 * 1000,
  });
}

export function useApproveSchedule(facultyId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (note?: string) => approveSchedule(facultyId, note),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['scheduleApproval'] });
    },
  });
}

export function useRevokeScheduleApproval(facultyId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => revokeScheduleApproval(facultyId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['scheduleApproval'] });
    },
  });
}
