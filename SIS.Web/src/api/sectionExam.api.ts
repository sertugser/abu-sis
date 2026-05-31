import apiClient from './axios';
import type { SectionExam, SaveSectionExamRequest, ExamConfig } from '../types/sectionExam';
import type { GradeComponentType } from '../types/grade';

const base = (sectionId: number) => `/instructor/section/${sectionId}/exams`;

export async function getSectionExams(sectionId: number): Promise<SectionExam[]> {
  const { data } = await apiClient.get<SectionExam[]>(base(sectionId));
  return data;
}

export async function saveSectionExam(sectionId: number, dto: SaveSectionExamRequest): Promise<SectionExam> {
  const { data } = await apiClient.post<SectionExam>(base(sectionId), dto);
  return data;
}

export async function deleteSectionExam(sectionId: number, examId: number): Promise<void> {
  await apiClient.delete(`${base(sectionId)}/${examId}`);
}

export async function getExamConfig(sectionId: number): Promise<ExamConfig> {
  const { data } = await apiClient.get<ExamConfig>(`${base(sectionId)}/config`);
  return data;
}

// Ortak GradeComponent upsert'i (mevcut uç) — modal etki oranı/ad/kısa ad/tür için kullanır.
export async function upsertComponent(
  sectionId: number,
  dto: { id: number | null; name: string; weight: number; order: number; type: GradeComponentType; shortName: string }
): Promise<{ id: number }> {
  const { data } = await apiClient.post(`/instructor/section/${sectionId}/components`, dto);
  return data;
}
