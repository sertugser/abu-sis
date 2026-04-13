import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getActiveCourseIntentionSurvey,
  getCourseIntentionSummary,
  getCourseIntentionSurveyForStudent,
  getCourseIntentionSurveys,
  saveCourseIntentions,
  setCourseIntentionSurveyState,
  upsertCourseIntentionSurvey,
} from '../api/courseIntention.api';
import { QUERY_KEYS } from '../lib/constants';
import type {
  SaveCourseIntentionsRequest,
  SetCourseIntentionSurveyStateRequest,
  UpsertCourseIntentionSurveyRequest,
} from '../types/courseIntention';

export function useActiveCourseIntentionSurvey() {
  return useQuery({
    queryKey: QUERY_KEYS.courseIntentionActive,
    queryFn: getActiveCourseIntentionSurvey,
    retry: false,
  });
}

export function useCourseIntentionSurvey(surveyId: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.courseIntentionSurvey(surveyId ?? 0),
    queryFn: () => getCourseIntentionSurveyForStudent(surveyId!),
    enabled: surveyId !== null && surveyId > 0,
  });
}

export function useSaveCourseIntentions() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SaveCourseIntentionsRequest) => saveCourseIntentions(request),
    onSuccess: (data) => {
      queryClient.setQueryData(QUERY_KEYS.courseIntentionActive, data);
      queryClient.setQueryData(QUERY_KEYS.courseIntentionSurvey(data.survey.id), data);
      queryClient.invalidateQueries({ queryKey: ['courseIntention'] });
    },
  });
}

export function useCourseIntentionSurveys(termId?: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.courseIntentionSurveys(termId ?? undefined),
    queryFn: () => getCourseIntentionSurveys(termId),
  });
}

export function useUpsertCourseIntentionSurvey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpsertCourseIntentionSurveyRequest) => upsertCourseIntentionSurvey(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['courseIntention'] });
    },
  });
}

export function useSetCourseIntentionSurveyState() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ surveyId, request }: { surveyId: number; request: SetCourseIntentionSurveyStateRequest }) =>
      setCourseIntentionSurveyState(surveyId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['courseIntention'] });
    },
  });
}

export function useCourseIntentionSummary(surveyId: number | null, departmentId?: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.courseIntentionSummary(surveyId ?? undefined, departmentId ?? undefined),
    queryFn: () => getCourseIntentionSummary(surveyId!, departmentId),
    enabled: surveyId !== null && surveyId > 0,
  });
}
