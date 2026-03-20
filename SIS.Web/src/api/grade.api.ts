// SIS.Web/src/api/grade.api.ts
import apiClient from './axios';
import type {
  GradeComponent,
  GradeSubmitDto,
  GradesResponse,
  GradingCriteria,
  FinalizeResponse,
} from '../types/grade';

// Bileşen CRUD
export async function getComponents(sectionId: number): Promise<GradeComponent[]> {
  const { data } = await apiClient.get(`/instructor/section/${sectionId}/components`);
  return data.components;
}

export async function addComponent(
  sectionId: number,
  component: { name: string; weight: number; order: number }
): Promise<void> {
  await apiClient.post(`/instructor/section/${sectionId}/components`, component);
}

export async function deleteComponent(sectionId: number, componentId: number): Promise<void> {
  await apiClient.delete(`/instructor/section/${sectionId}/components/${componentId}`);
}

// Not girişi
export async function getGrades(sectionId: number): Promise<GradesResponse> {
  const { data } = await apiClient.get(`/instructor/section/${sectionId}/grades`);
  return data;
}

export async function submitGrades(sectionId: number, dto: GradeSubmitDto): Promise<void> {
  await apiClient.post(`/instructor/section/${sectionId}/grades`, dto);
}

// Kesinleştirme
export async function finalizeSection(sectionId: number): Promise<FinalizeResponse> {
  const { data } = await apiClient.post(`/instructor/section/${sectionId}/finalize`);
  return data;
}

export async function unfinalizeSection(sectionId: number): Promise<void> {
  await apiClient.post(`/instructor/section/${sectionId}/unfinalize`);
}

// Harf aralığı
export async function getGradingCriteria(sectionId: number): Promise<GradingCriteria> {
  const { data } = await apiClient.get(`/instructor/section/${sectionId}/grading-criteria`);
  return data;
}

export async function updateGradingCriteria(
  sectionId: number,
  criteria: GradingCriteria
): Promise<void> {
  await apiClient.put(`/instructor/section/${sectionId}/grading-criteria`, criteria);
}
