// SIS.Web/src/hooks/useGradeData.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  getComponents,
  addComponent,
  deleteComponent,
  getGrades,
  submitGrades,
  finalizeSection,
  unfinalizeSection,
  getGradingCriteria,
  updateGradingCriteria,
} from '../api/grade.api';
import type { GradeSubmitDto, GradingCriteria } from '../types/grade';

export function useComponents(sectionId: number) {
  return useQuery({
    queryKey: ['gradeComponents', sectionId],
    queryFn: () => getComponents(sectionId),
    staleTime: 60 * 1000,
  });
}

export function useGrades(sectionId: number) {
  return useQuery({
    queryKey: ['grades', sectionId],
    queryFn: () => getGrades(sectionId),
    staleTime: 60 * 1000,
  });
}

export function useGradingCriteria(sectionId: number) {
  return useQuery({
    queryKey: ['gradingCriteria', sectionId],
    queryFn: () => getGradingCriteria(sectionId),
    staleTime: 2 * 60 * 1000,
  });
}

export function useAddComponent(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (component: { name: string; weight: number; order: number }) =>
      addComponent(sectionId, component),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['gradeComponents', sectionId] });
      qc.invalidateQueries({ queryKey: ['grades', sectionId] });
    },
  });
}

export function useDeleteComponent(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (componentId: number) => deleteComponent(sectionId, componentId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['gradeComponents', sectionId] });
      qc.invalidateQueries({ queryKey: ['grades', sectionId] });
    },
  });
}

export function useSubmitGrades(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: GradeSubmitDto) => submitGrades(sectionId, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['grades', sectionId] });
    },
  });
}

export function useFinalize(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => finalizeSection(sectionId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['grades', sectionId] });
    },
  });
}

export function useUnfinalize(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => unfinalizeSection(sectionId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['grades', sectionId] });
    },
  });
}

export function useUpdateGradingCriteria(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (criteria: GradingCriteria) => updateGradingCriteria(sectionId, criteria),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['gradingCriteria', sectionId] });
    },
  });
}
