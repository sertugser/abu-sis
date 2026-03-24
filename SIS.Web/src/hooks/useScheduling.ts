import { useMutation, useQueryClient } from '@tanstack/react-query';
import { batchAutoSchedule } from '../api/scheduling.api';
import type { BatchAutoScheduleRequest } from '../types/scheduling';

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
