import { useMutation, useQueryClient, useQuery } from '@tanstack/react-query';
import { batchAutoSchedule, getClassroomDashboard, getSchedulingConflicts } from '../api/scheduling.api';
import type { BatchAutoScheduleRequest, ClassroomDashboard, SchedulingConflict } from '../types/scheduling';

export function useClassroomDashboard(termId?: number) {
  return useQuery<ClassroomDashboard>({
    queryKey: ['classroomDashboard', termId],
    queryFn: () => getClassroomDashboard(termId),
    enabled: !!termId,
  });
}

export function useSchedulingConflicts(termId?: number) {
  return useQuery<SchedulingConflict[]>({
    queryKey: ['schedulingConflicts', termId],
    queryFn: () => getSchedulingConflicts(termId),
    enabled: !!termId,
  });
}

export function useBatchAutoSchedule() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: BatchAutoScheduleRequest) => batchAutoSchedule(request),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['departmentClassLists'] }),
        queryClient.invalidateQueries({ queryKey: ['academicPlanningSummary'] }),
      ]);
    },
  });
}
