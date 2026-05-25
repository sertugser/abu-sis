import apiClient from './axios';
import type { ExamRegulation, SaveExamRegulationRequest } from '../types/examRegulation';

const API = '/admin/exam-regulations';

export async function getExamRegulations(programId?: number) {
  const { data } = await apiClient.get<ExamRegulation[]>(API, {
    params: programId ? { programId } : undefined,
  });
  return data;
}

export async function createExamRegulation(dto: SaveExamRegulationRequest) {
  const { data } = await apiClient.post<ExamRegulation>(API, dto);
  return data;
}

export async function updateExamRegulation(id: number, dto: SaveExamRegulationRequest) {
  const { data } = await apiClient.put<ExamRegulation>(`${API}/${id}`, dto);
  return data;
}

export async function deleteExamRegulation(id: number) {
  await apiClient.delete(`${API}/${id}`);
}

export async function cloneExamRegulation(id: number, payload: { programId?: number; year: number }) {
  const { data } = await apiClient.post<ExamRegulation>(`${API}/${id}/clone`, payload);
  return data;
}
