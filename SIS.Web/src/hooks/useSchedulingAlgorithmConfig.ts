import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getSchedulingAlgorithmConfigLimits,
  getSchedulingAlgorithmConfigs,
  updateSchedulingAlgorithmConfig,
} from '../api/schedulingAlgorithmConfig.api';

const QUERY_KEY = ['admin', 'scheduling-algorithm-config'] as const;
const LIMITS_KEY = ['admin', 'scheduling-algorithm-config', 'limits'] as const;

export function useSchedulingAlgorithmConfigs() {
  return useQuery({
    queryKey: QUERY_KEY,
    queryFn: getSchedulingAlgorithmConfigs,
    staleTime: 60 * 1000,
  });
}

export function useSchedulingAlgorithmConfigLimits() {
  return useQuery({
    queryKey: LIMITS_KEY,
    queryFn: getSchedulingAlgorithmConfigLimits,
    staleTime: 5 * 60 * 1000,
  });
}

export function useUpdateSchedulingAlgorithmConfig() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, value }: { id: number; value: string }) => updateSchedulingAlgorithmConfig(id, value),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEY });
    },
  });
}
