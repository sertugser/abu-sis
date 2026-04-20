import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  closePeriod,
  createQuestion,
  deleteQuestion,
  getActivePeriod,
  getActiveQuestions,
  getAllQuestions,
  getDepartmentSummary,
  getMyEvaluableCourses,
  getSectionResults,
  openPeriod,
  submitEvaluation,
  updateQuestion,
} from '../api/courseEvaluation.api';
import { QUERY_KEYS } from '../lib/constants';
import type {
  CreateEvaluationQuestionPayload,
  OpenPeriodPayload,
  SubmitEvaluationPayload,
  UpdateEvaluationQuestionPayload,
} from '../types/courseEvaluation';

export function useActiveEvaluationPeriod() {
  return useQuery({
    queryKey: QUERY_KEYS.evaluationActivePeriod,
    queryFn: getActivePeriod,
    staleTime: 60 * 1000,
  });
}

export function useMyEvaluableCourses(enabled = true) {
  return useQuery({
    queryKey: QUERY_KEYS.evaluationMyCourses,
    queryFn: getMyEvaluableCourses,
    staleTime: 30 * 1000,
    enabled,
  });
}

export function useActiveEvaluationQuestions(enabled = true) {
  return useQuery({
    queryKey: QUERY_KEYS.evaluationActiveQuestions,
    queryFn: getActiveQuestions,
    staleTime: 5 * 60 * 1000,
    enabled,
  });
}

export function useSubmitEvaluation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: SubmitEvaluationPayload) => submitEvaluation(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.evaluationMyCourses });
    },
  });
}

export function useSectionEvaluationResults(sectionId: number | null, enabled = true) {
  return useQuery({
    queryKey: QUERY_KEYS.evaluationSectionResults(sectionId ?? 0),
    queryFn: () => getSectionResults(sectionId!),
    enabled: enabled && sectionId != null && sectionId > 0,
    staleTime: 60 * 1000,
  });
}

export function useDepartmentEvaluationSummary(
  departmentId: number | null,
  termId?: number,
  enabled = true
) {
  return useQuery({
    queryKey: QUERY_KEYS.evaluationDepartmentSummary(departmentId ?? 0, termId),
    queryFn: () => getDepartmentSummary(departmentId!, termId),
    enabled: enabled && departmentId != null && departmentId > 0,
    staleTime: 60 * 1000,
  });
}

export function useAllEvaluationQuestions(enabled = true) {
  return useQuery({
    queryKey: QUERY_KEYS.evaluationAllQuestions,
    queryFn: getAllQuestions,
    staleTime: 60 * 1000,
    enabled,
  });
}

export function useOpenEvaluationPeriod() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: OpenPeriodPayload) => openPeriod(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.evaluationActivePeriod });
    },
  });
}

export function useCloseEvaluationPeriod() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (periodId: number) => closePeriod(periodId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.evaluationActivePeriod });
    },
  });
}

export function useCreateEvaluationQuestion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: CreateEvaluationQuestionPayload) => createQuestion(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.evaluationAllQuestions });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.evaluationActiveQuestions });
    },
  });
}

export function useUpdateEvaluationQuestion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: UpdateEvaluationQuestionPayload }) =>
      updateQuestion(id, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.evaluationAllQuestions });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.evaluationActiveQuestions });
    },
  });
}

export function useDeleteEvaluationQuestion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => deleteQuestion(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.evaluationAllQuestions });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.evaluationActiveQuestions });
    },
  });
}
