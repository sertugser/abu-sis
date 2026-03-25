import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  applySchedule, assignSection, autoSchedulePreview, getAvailableClassrooms,
  getFacultyPlanningConflicts, getFacultyPlanningOverview, getFacultyPlanningSections, unassignSection,
} from '../api/facultyPlanning.api';
import type {
  ApplyScheduleRequest, AssignSectionRequest, AutoScheduleRequest,
} from '../types/facultyPlanning';
import { QUERY_KEYS } from '../lib/constants';

export function useFacultyPlanningOverview(facultyId: number | null, termId: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.facultyPlanningOverview(facultyId ?? 0, termId ?? 0),
    queryFn: () => getFacultyPlanningOverview(facultyId!, termId!),
    enabled: facultyId != null && termId != null,
    staleTime: 30 * 1000,
  });
}

export function useFacultyPlanningSections(
  facultyId: number | null, termId: number | null,
  departmentId?: number, status?: string, page = 1, pageSize = 50,
) {
  return useQuery({
    queryKey: QUERY_KEYS.facultyPlanningSections(facultyId ?? 0, termId ?? 0, departmentId, status, page),
    queryFn: () => getFacultyPlanningSections(facultyId!, termId!, departmentId, status, page, pageSize),
    enabled: facultyId != null && termId != null,
    staleTime: 30 * 1000,
  });
}

export function useAssignSection() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: AssignSectionRequest) => assignSection(req),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['facultyPlanning'] });
    },
  });
}

export function useUnassignSection() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (sectionId: number) => unassignSection(sectionId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['facultyPlanning'] });
    },
  });
}

export function useAutoSchedulePreview() {
  return useMutation({
    mutationFn: (req: AutoScheduleRequest) => autoSchedulePreview(req),
  });
}

export function useApplySchedule() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: ApplyScheduleRequest) => applySchedule(req),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['facultyPlanning'] });
    },
  });
}

export function useAvailableClassrooms(
  termId: number | null, day?: number, startTime?: string, endTime?: string,
  minCapacity?: number, type?: string,
) {
  return useQuery({
    queryKey: ['availableClassrooms', termId, day, startTime, endTime, minCapacity, type],
    queryFn: () => getAvailableClassrooms(termId!, day, startTime, endTime, minCapacity, type),
    enabled: termId != null,
    staleTime: 15 * 1000,
  });
}

export function useFacultyPlanningConflicts(facultyId: number | null, termId: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.facultyPlanningConflicts(facultyId ?? 0, termId ?? 0),
    queryFn: () => getFacultyPlanningConflicts(facultyId!, termId!),
    enabled: facultyId != null && termId != null,
    staleTime: 30 * 1000,
  });
}
