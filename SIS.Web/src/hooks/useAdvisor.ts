import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getPendingApprovals, approveStudent, rejectStudent } from '../api/advisor.api';

export function usePendingApprovals() {
  return useQuery({
    queryKey: ['advisor', 'pending-approvals'],
    queryFn: getPendingApprovals,
  });
}

export function useApproveStudent() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ studentId, note }: { studentId: number; note?: string }) =>
      approveStudent(studentId, note),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['advisor'] }),
  });
}

export function useRejectStudent() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ studentId, note }: { studentId: number; note?: string }) =>
      rejectStudent(studentId, note),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['advisor'] }),
  });
}
