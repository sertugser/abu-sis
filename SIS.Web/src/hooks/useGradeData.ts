// SIS.Web/src/hooks/useGradeData.ts
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  addComponent,
  applyImport,
  deleteComponent,
  downloadTemplate,
  finalizeButunleme,
  finalizeMazeret,
  finalizeSection,
  getButunlemeRoster,
  getComponents,
  getGrades,
  getGradingCriteria,
  getMazeretRoster,
  previewImport,
  publishComponent,
  setButunlemeScores,
  setExamAttendance,
  setMazeretScores,
  submitGrades,
  unfinalizeSection,
  unpublishComponent,
  updateGradingCriteria,
} from '../api/grade.api';
import type {
  ButunlemeScoreItem,
  ExamAttendance,
  GradeComponentType,
  GradeSubmitDto,
  GradingCriteria,
  MazeretScoreItem,
} from '../types/grade';

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
    mutationFn: (component: { name: string; weight: number; order: number; type: GradeComponentType }) =>
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

// Komponent ilan (publish/unpublish). Öğrenci görünürlüğü için tek-kaynak hoca kapısı.
// Section'a değil komponente bağlı — aynı dersin tüm section'ları ortak komponent paylaşır,
// dolayısıyla diğer section'ların hocaları için de aynı ilan görünür.
export function usePublishComponent(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (componentId: number) => publishComponent(componentId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['gradeComponents', sectionId] });
    },
  });
}

export function useUnpublishComponent(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (componentId: number) => unpublishComponent(componentId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['gradeComponents', sectionId] });
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
    mutationFn: (componentId: number) => downloadTemplate(sectionId, componentId),
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

export function useSetExamAttendance(sectionId: number) {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: ({
      componentId,
      items,
    }: {
      componentId: number;
      items: { studentId: number; attendance: ExamAttendance }[];
    }) => setExamAttendance(sectionId, componentId, items),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['grades', sectionId] });
    },
  });
}

// ── Bütünleme hooks ──────────────────────────────────────

export function useButunlemeRoster(sectionId: number, enabled: boolean) {
  return useQuery({
    queryKey: ['butunlemeRoster', sectionId],
    queryFn: () => getButunlemeRoster(sectionId),
    enabled,
    staleTime: 30 * 1000,
  });
}

export function useSetButunlemeScores(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (items: ButunlemeScoreItem[]) => setButunlemeScores(sectionId, items),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['butunlemeRoster', sectionId] });
    },
  });
}

export function useFinalizeButunleme(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => finalizeButunleme(sectionId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['butunlemeRoster', sectionId] });
      qc.invalidateQueries({ queryKey: ['grades', sectionId] });
    },
  });
}

// ── Mazeret hooks ────────────────────────────────────────

export function useMazeretRoster(sectionId: number, enabled: boolean) {
  return useQuery({
    queryKey: ['mazeretRoster', sectionId],
    queryFn: () => getMazeretRoster(sectionId),
    enabled,
    staleTime: 30 * 1000,
  });
}

export function useSetMazeretScores(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (items: MazeretScoreItem[]) => setMazeretScores(sectionId, items),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['mazeretRoster', sectionId] });
    },
  });
}

export function useFinalizeMazeret(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => finalizeMazeret(sectionId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['mazeretRoster', sectionId] });
      qc.invalidateQueries({ queryKey: ['grades', sectionId] });
    },
  });
}
