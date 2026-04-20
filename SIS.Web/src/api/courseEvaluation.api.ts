import apiClient from './axios';
import type {
  ActivePeriodResponse,
  CreateEvaluationQuestionPayload,
  DepartmentEvaluationSummary,
  EvaluableCourse,
  EvaluationPeriod,
  EvaluationQuestion,
  OpenPeriodPayload,
  SectionEvaluationResult,
  SubmitEvaluationPayload,
  UpdateEvaluationQuestionPayload,
} from '../types/courseEvaluation';

export async function getActivePeriod(): Promise<ActivePeriodResponse> {
  const { data } = await apiClient.get<ActivePeriodResponse>('/course-evaluation/period/active');
  return data;
}

export async function getMyEvaluableCourses(): Promise<EvaluableCourse[]> {
  const { data } = await apiClient.get<EvaluableCourse[]>('/course-evaluation/my-courses');
  return data;
}

export async function getActiveQuestions(): Promise<EvaluationQuestion[]> {
  const { data } = await apiClient.get<EvaluationQuestion[]>('/course-evaluation/questions');
  return data;
}

export async function submitEvaluation(payload: SubmitEvaluationPayload): Promise<void> {
  await apiClient.post('/course-evaluation/submit', payload);
}

export async function getSectionResults(sectionId: number): Promise<SectionEvaluationResult> {
  const { data } = await apiClient.get<SectionEvaluationResult>(
    `/course-evaluation/results/section/${sectionId}`
  );
  return data;
}

export async function getDepartmentSummary(
  departmentId: number,
  termId?: number
): Promise<DepartmentEvaluationSummary> {
  const { data } = await apiClient.get<DepartmentEvaluationSummary>(
    `/course-evaluation/results/department/${departmentId}`,
    { params: { termId } }
  );
  return data;
}

export async function openPeriod(payload: OpenPeriodPayload): Promise<EvaluationPeriod> {
  const { data } = await apiClient.post<EvaluationPeriod>('/course-evaluation/period/open', payload);
  return data;
}

export async function closePeriod(periodId: number): Promise<EvaluationPeriod> {
  const { data } = await apiClient.post<EvaluationPeriod>(
    `/course-evaluation/period/${periodId}/close`
  );
  return data;
}

export async function getAllQuestions(): Promise<EvaluationQuestion[]> {
  const { data } = await apiClient.get<EvaluationQuestion[]>('/course-evaluation/questions/all');
  return data;
}

export async function createQuestion(
  payload: CreateEvaluationQuestionPayload
): Promise<EvaluationQuestion> {
  const { data } = await apiClient.post<EvaluationQuestion>('/course-evaluation/questions', payload);
  return data;
}

export async function updateQuestion(
  id: number,
  payload: UpdateEvaluationQuestionPayload
): Promise<EvaluationQuestion> {
  const { data } = await apiClient.put<EvaluationQuestion>(
    `/course-evaluation/questions/${id}`,
    payload
  );
  return data;
}

export async function deleteQuestion(id: number): Promise<void> {
  await apiClient.delete(`/course-evaluation/questions/${id}`);
}
