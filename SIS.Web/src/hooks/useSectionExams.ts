import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getSectionExams, saveSectionExam, deleteSectionExam, getExamConfig, upsertComponent,
} from '../api/sectionExam.api';
import type { SaveSectionExamRequest } from '../types/sectionExam';
import type { GradeComponentType } from '../types/grade';

const ROOT = ['sectionExams'] as const;

export function useSectionExams(sectionId: number | null) {
  return useQuery({
    queryKey: [...ROOT, 'list', sectionId],
    queryFn: () => getSectionExams(sectionId as number),
    enabled: sectionId != null,
    staleTime: 60 * 1000,
  });
}

export function useExamConfig(sectionId: number | null) {
  return useQuery({
    queryKey: [...ROOT, 'config', sectionId],
    queryFn: () => getExamConfig(sectionId as number),
    enabled: sectionId != null,
    staleTime: 60 * 1000,
  });
}

export function useSaveSectionExam(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: SaveSectionExamRequest) => saveSectionExam(sectionId, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: [...ROOT, 'list', sectionId] });
      qc.invalidateQueries({ queryKey: [...ROOT, 'config', sectionId] });
    },
  });
}

export function useDeleteSectionExam(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (examId: number) => deleteSectionExam(sectionId, examId),
    onSuccess: () => qc.invalidateQueries({ queryKey: [...ROOT, 'list', sectionId] }),
  });
}

export function useUpsertComponent(sectionId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: { id: number | null; name: string; weight: number; order: number; type: GradeComponentType; shortName: string }) =>
      upsertComponent(sectionId, dto),
    onSuccess: () => qc.invalidateQueries({ queryKey: [...ROOT, 'config', sectionId] }),
  });
}
