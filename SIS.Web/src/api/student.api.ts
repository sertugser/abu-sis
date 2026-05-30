import apiClient from './axios';
import type { StudentGradeStatisticsDto, StudentInfoDto, TermGpaHistoryDto } from '../types/student';
import type { CurriculumStatusDto } from '../types/curriculum';
import type { StudentTranscriptDto } from '../types/transcript';
import type { ScenarioReport, TranscriptScenarioRequest } from '../types/transcriptScenario';

export async function getStudentInfo(): Promise<StudentInfoDto> {
  const response = await apiClient.get<StudentInfoDto>('/student/me');
  return response.data;
}

export async function getCurriculumStatus(): Promise<CurriculumStatusDto> {
  const response = await apiClient.get<CurriculumStatusDto>('/student/curriculum-status');
  return response.data;
}

export async function getTermGpaHistory(): Promise<TermGpaHistoryDto> {
  const response = await apiClient.get<TermGpaHistoryDto>('/student/term-gpa-history');
  return response.data;
}

export async function getStudentTranscript(): Promise<StudentTranscriptDto> {
  const response = await apiClient.get<StudentTranscriptDto>('/student/transcript');
  return response.data;
}

// Transkript Senaryosu (what-if): hesap backend'de TEK kaynaktan yapılır.
// Sistemde kalıcı değişiklik YOK — salt bilgilendirme.
export async function calculateTranscriptScenario(
  request: TranscriptScenarioRequest,
): Promise<ScenarioReport> {
  const response = await apiClient.post<ScenarioReport>('/student/transcript/scenario', request);
  return response.data;
}

export async function getStudentGradeStatistics(registrationId: number, commonEvaluation = false): Promise<StudentGradeStatisticsDto> {
  const response = await apiClient.get<StudentGradeStatisticsDto>(
    `/student/registrations/${registrationId}/grade-statistics`,
    { params: commonEvaluation ? { commonEvaluation } : undefined }
  );
  return response.data;
}
