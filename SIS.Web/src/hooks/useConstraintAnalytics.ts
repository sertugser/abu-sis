import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  takeSnapshot,
  getSnapshots,
  getTrendData,
  compareSnapshots,
  getHeatmap,
  getBottlenecks,
} from '../api/constraintAnalytics.api';

const ANALYTICS_KEYS = {
  snapshots: (termId: number) => ['constraintAnalytics', 'snapshots', termId] as const,
  trend: (termId: number) => ['constraintAnalytics', 'trend', termId] as const,
  compare: (id1: number, id2: number) => ['constraintAnalytics', 'compare', id1, id2] as const,
  heatmap: (termId: number) => ['constraintAnalytics', 'heatmap', termId] as const,
  bottlenecks: (termId: number) => ['constraintAnalytics', 'bottlenecks', termId] as const,
};

export function useSnapshots(termId: number | null) {
  return useQuery({
    queryKey: ANALYTICS_KEYS.snapshots(termId!),
    queryFn: () => getSnapshots(termId!),
    enabled: termId != null,
    staleTime: 30_000,
  });
}

export function useTrendData(termId: number | null) {
  return useQuery({
    queryKey: ANALYTICS_KEYS.trend(termId!),
    queryFn: () => getTrendData(termId!),
    enabled: termId != null,
    staleTime: 30_000,
  });
}

export function useSnapshotComparison(id1: number | null, id2: number | null) {
  return useQuery({
    queryKey: ANALYTICS_KEYS.compare(id1!, id2!),
    queryFn: () => compareSnapshots(id1!, id2!),
    enabled: id1 != null && id2 != null && id1 !== id2,
    staleTime: 60_000,
  });
}

export function useHeatmap(termId: number | null) {
  return useQuery({
    queryKey: ANALYTICS_KEYS.heatmap(termId!),
    queryFn: () => getHeatmap(termId!),
    enabled: termId != null,
    staleTime: 30_000,
  });
}

export function useBottlenecks(termId: number | null) {
  return useQuery({
    queryKey: ANALYTICS_KEYS.bottlenecks(termId!),
    queryFn: () => getBottlenecks(termId!),
    enabled: termId != null,
    staleTime: 30_000,
  });
}

export function useTakeSnapshot(termId: number | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => takeSnapshot(termId!),
    onSuccess: () => {
      if (termId != null) {
        qc.invalidateQueries({ queryKey: ANALYTICS_KEYS.snapshots(termId) });
        qc.invalidateQueries({ queryKey: ANALYTICS_KEYS.trend(termId) });
      }
    },
  });
}
