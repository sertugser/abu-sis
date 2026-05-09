import { useQuery } from '@tanstack/react-query';
import {
  getClassroomDashboard, getSchedulingConflicts,
  getClassroomFilterOptions, searchAvailableClassrooms,
  type ClassroomDashboardParams,
} from '../api/scheduling.api';
import type {
  ClassroomDashboard, SchedulingConflict,
  ClassroomFilterOptions, AvailableClassroomResult, AvailableClassroomSearchParams,
} from '../types/scheduling';

export function useClassroomDashboard(params: ClassroomDashboardParams) {
  return useQuery<ClassroomDashboard>({
    queryKey: ['classroomDashboard', params],
    queryFn: () => getClassroomDashboard(params),
    enabled: !!params.termId,
  });
}

export function useSchedulingConflicts(termId?: number) {
  return useQuery<SchedulingConflict[]>({
    queryKey: ['schedulingConflicts', termId],
    queryFn: () => getSchedulingConflicts(termId),
    enabled: !!termId,
  });
}

export function useClassroomFilterOptions(termId?: number) {
  return useQuery<ClassroomFilterOptions>({
    queryKey: ['classroomFilterOptions', termId],
    queryFn: () => getClassroomFilterOptions(termId!),
    enabled: !!termId,
  });
}

export function useAvailableClassrooms(params: AvailableClassroomSearchParams | null) {
  return useQuery<AvailableClassroomResult[]>({
    queryKey: ['availableClassrooms', params],
    queryFn: () => searchAvailableClassrooms(params!),
    enabled: !!params && params.days.length > 0,
  });
}
