import apiClient from './axios';
import type {
  CourseIntentionSurveyDetailDto,
  CourseIntentionSurveyDto,
  CourseIntentionSummaryDto,
  SaveCourseIntentionsRequest,
  SetCourseIntentionSurveyStateRequest,
  UpsertCourseIntentionSurveyRequest,
} from '../types/courseIntention';

export async function getActiveCourseIntentionSurvey(): Promise<CourseIntentionSurveyDetailDto> {
  const response = await apiClient.get<CourseIntentionSurveyDetailDto>('/course-intention/active');
  return response.data;
}

export async function getCourseIntentionSurveyForStudent(surveyId: number): Promise<CourseIntentionSurveyDetailDto> {
  const response = await apiClient.get<CourseIntentionSurveyDetailDto>(`/course-intention/surveys/${surveyId}/me`);
  return response.data;
}

export async function saveCourseIntentions(request: SaveCourseIntentionsRequest): Promise<CourseIntentionSurveyDetailDto> {
  const response = await apiClient.post<CourseIntentionSurveyDetailDto>('/course-intention/save', request);
  return response.data;
}

export async function getCourseIntentionSurveys(termId?: number | null): Promise<CourseIntentionSurveyDto[]> {
  const response = await apiClient.get<CourseIntentionSurveyDto[]>('/course-intention/surveys', {
    params: termId ? { termId } : undefined,
  });
  return response.data;
}

export async function upsertCourseIntentionSurvey(
  request: UpsertCourseIntentionSurveyRequest
): Promise<CourseIntentionSurveyDto> {
  const response = await apiClient.post<CourseIntentionSurveyDto>('/course-intention/surveys', request);
  return response.data;
}

export async function setCourseIntentionSurveyState(
  surveyId: number,
  request: SetCourseIntentionSurveyStateRequest
): Promise<CourseIntentionSurveyDto> {
  const response = await apiClient.post<CourseIntentionSurveyDto>(
    `/course-intention/surveys/${surveyId}/state`,
    request
  );
  return response.data;
}

export async function getCourseIntentionSummary(
  surveyId: number,
  departmentId?: number | null
): Promise<CourseIntentionSummaryDto> {
  const response = await apiClient.get<CourseIntentionSummaryDto>(`/course-intention/surveys/${surveyId}/summary`, {
    params: departmentId ? { departmentId } : undefined,
  });
  return response.data;
}
