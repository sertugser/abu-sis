import apiClient from './axios';
import type {
  SnapshotDto,
  TrendDataDto,
  SnapshotComparisonDto,
  HeatmapDto,
  BottleneckDto,
} from '../types/constraintAnalytics';

const BASE = '/constraints/analytics';

export async function takeSnapshot(termId: number): Promise<SnapshotDto> {
  const { data } = await apiClient.post<SnapshotDto>(`${BASE}/snapshot?termId=${termId}`);
  return data;
}

export async function getSnapshots(termId: number, limit = 50): Promise<SnapshotDto[]> {
  const { data } = await apiClient.get<SnapshotDto[]>(`${BASE}/snapshots?termId=${termId}&limit=${limit}`);
  return data;
}

export async function getTrendData(termId: number): Promise<TrendDataDto> {
  const { data } = await apiClient.get<TrendDataDto>(`${BASE}/trend?termId=${termId}`);
  return data;
}

export async function compareSnapshots(id1: number, id2: number): Promise<SnapshotComparisonDto> {
  const { data } = await apiClient.get<SnapshotComparisonDto>(`${BASE}/compare?id1=${id1}&id2=${id2}`);
  return data;
}

export async function getHeatmap(termId: number): Promise<HeatmapDto> {
  const { data } = await apiClient.get<HeatmapDto>(`${BASE}/heatmap?termId=${termId}`);
  return data;
}

export async function getBottlenecks(termId: number): Promise<BottleneckDto> {
  const { data } = await apiClient.get<BottleneckDto>(`${BASE}/bottlenecks?termId=${termId}`);
  return data;
}
