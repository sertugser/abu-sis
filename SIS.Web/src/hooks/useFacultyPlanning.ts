import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  applySchedule, assignSection, autoSchedulePreview, batchUnassignSections,
  getAvailableClassrooms, getFacultyPlanningConflicts, getFacultyPlanningOverview,
  getFacultyPlanningSections, getFacultyPlanningTimeSlots, getScheduleRun,
  getScheduleRuns, resetAllSchedules, unassignSection, validateSectionSchedule,
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
  departmentId?: number, status?: string, page = 1, pageSize = 50, search?: string,
) {
  return useQuery({
    queryKey: QUERY_KEYS.facultyPlanningSections(facultyId ?? 0, termId ?? 0, departmentId, status, page, pageSize, search),
    queryFn: () => getFacultyPlanningSections(facultyId!, termId!, departmentId, status, page, pageSize, search),
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
      qc.invalidateQueries({ queryKey: ['facultyPlanningConflicts'] });
    },
  });
}

export function useUnassignSection() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (sectionId: number) => unassignSection(sectionId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['facultyPlanning'] });
      qc.invalidateQueries({ queryKey: ['facultyPlanningConflicts'] });
    },
  });
}

export function useAutoSchedulePreview() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: AutoScheduleRequest) => autoSchedulePreview(req),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['facultyPlanning', 'scheduleRuns'] });
    },
  });
}

export function useScheduleRuns(facultyId: number, termId: number | null) {
  return useQuery({
    queryKey: ['facultyPlanning', 'scheduleRuns', facultyId, termId],
    queryFn: () => getScheduleRuns(facultyId, termId!),
    enabled: termId != null,
    staleTime: 30 * 1000,
  });
}

export function useScheduleRun(id: number | null) {
  return useQuery({
    queryKey: ['facultyPlanning', 'scheduleRun', id],
    queryFn: () => getScheduleRun(id!),
    enabled: id != null,
    staleTime: 60 * 1000,
  });
}

export function useApplySchedule() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: ApplyScheduleRequest) => applySchedule(req),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['facultyPlanning'] });
      qc.invalidateQueries({ queryKey: ['facultyPlanningConflicts'] });
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
    staleTime: 0,
  });
}

export function useFacultyPlanningConflicts(facultyId: number | null, termId: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.facultyPlanningConflicts(facultyId ?? 0, termId ?? 0),
    queryFn: () => getFacultyPlanningConflicts(facultyId!, termId!),
    enabled: facultyId != null && termId != null,
    staleTime: 0,
  });
}

export function useFacultyPlanningTimeSlots(termId: number | null) {
  return useQuery({
    queryKey: ['facultyPlanning', 'timeSlots', termId],
    queryFn: () => getFacultyPlanningTimeSlots(termId!),
    enabled: termId != null,
    staleTime: 60 * 1000,
  });
}

export function useValidateSectionSchedule() {
  return useMutation({
    mutationFn: validateSectionSchedule,
  });
}

export function useBatchUnassignSections() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: batchUnassignSections,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['facultyPlanning'] });
      queryClient.invalidateQueries({ queryKey: ['facultyPlanningConflicts'] });
    },
  });
}

export function useResetAllSchedules() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ facultyId, termId }: { facultyId: number; termId: number }) =>
      resetAllSchedules(facultyId, termId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['facultyPlanning'] });
      queryClient.invalidateQueries({ queryKey: ['facultyPlanningConflicts'] });
    },
  });
}
