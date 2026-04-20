import apiClient from './axios';
import type {
  CourseDifficultyDto,
  CriticalPathAnalysisDto,
  StudentRecommendationsDto,
} from '../types/recommendation';

export async function getMyRecommendations(pacePreference = 50): Promise<StudentRecommendationsDto> {
  const response = await apiClient.get<StudentRecommendationsDto>('/recommendation/my', {
    params: { pacePreference },
  });
  return response.data;
}

export async function getCourseDifficulty(courseId: number): Promise<CourseDifficultyDto> {
  const response = await apiClient.get<CourseDifficultyDto>(`/recommendation/difficulty/${courseId}`);
  return response.data;
}

export async function getCriticalPath(courseId: number): Promise<CriticalPathAnalysisDto> {
  const response = await apiClient.get<CriticalPathAnalysisDto>(`/recommendation/critical-path/${courseId}`);
  return response.data;
}
