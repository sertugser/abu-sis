// SIS.Web/src/hooks/useGradeData.ts
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  addComponent,
  applyImport,
  deleteComponent,
  downloadTemplate,
  finalizeSection,
  getComponents,
  getGrades,
  getGradingCriteria,
  previewImport,
  publishGrades,
  submitGrades,
  unfinalizeSection,
  unpublishGrades,
  updateGradingCriteria,
} from '../api/grade.api';
import type {
  GradeSubmitDto,
  GradingCriteria,
} from '../types/grade';

export function useComponents(sectionId: number) {
  return useQuery({
    queryKey: ['gradeComponents', sectionId],
    queryFn: () => getComponents(sectionId),
    staleTime: 0,
    refetchOnMount: 'always',
  });
}

export function useGrades(sectionId: number) {
  return useQuery({
    queryKey: ['grades', sectionId],
    queryFn: () => getGrades(sectionId),
    staleTime: 0,
    refetchOnMount: 'always',
  });
}

export function useGradingCriteria(sectionId: number) {
  return useQuery({
    queryKey: ['gradingCriteria', sectionId],
    queryFn: () => getGradingCriteria(sectionId),
    staleTime: 0,
    refetchOnMount: 'always',
  });
}

export function useAddComponent(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (component: { id?: number | null; name: string; weight: number; order: number }) =>
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

export function usePublishGrades(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => publishGrades(sectionId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['grades', sectionId] });
      qc.invalidateQueries({ queryKey: ['instructorSections'] });
    },
  });
}

export function useUnpublishGrades(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => unpublishGrades(sectionId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['grades', sectionId] });
      qc.invalidateQueries({ queryKey: ['instructorSections'] });
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

export function useDownloadTemplate(sectionId: number) {
  return useMutation({
    mutationFn: () => downloadTemplate(sectionId),
  });
}

export function usePreviewImport(sectionId: number) {
  return useMutation({
    mutationFn: (file: File) => previewImport(sectionId, file),
  });
}

export function useApplyImport(sectionId: number) {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (file: File) => applyImport(sectionId, file),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['grades', sectionId] });
      qc.invalidateQueries({ queryKey: ['gradeComponents', sectionId] });
    },
  });
}
