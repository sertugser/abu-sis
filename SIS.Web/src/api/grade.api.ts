// SIS.Web/src/api/grade.api.ts
import apiClient from './axios';
import type {
  FinalizeResponse,
  GradeComponent,
  GradeComponentType,
  GradeSubmitDto,
  GradesResponse,
  GradingCriteria,
  ImportApplyResponse,
  ImportPreviewResponse,
} from '../types/grade';

// Bileşen CRUD
export async function getComponents(sectionId: number): Promise<GradeComponent[]> {
  const { data } = await apiClient.get(`/instructor/section/${sectionId}/components`);
  return data.components;
}

export async function addComponent(
  sectionId: number,
  component: { name: string; weight: number; order: number; type: GradeComponentType }
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

// Excel template indir
export async function downloadTemplate(sectionId: number, componentId: number): Promise<Blob> {
  const { data } = await apiClient.get(
    `/instructor/section/${sectionId}/grades/export-template`,
    {
      params: { componentId },
      responseType: 'blob',
    }
  );

  return data;
}

// Excel preview
export async function previewImport(
  sectionId: number,
  file: File
): Promise<ImportPreviewResponse> {
  const formData = new FormData();
  formData.append('file', file);

  const { data } = await apiClient.post(
    `/instructor/section/${sectionId}/grades/import-preview`,
    formData,
    {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    }
  );

  return data;
}

// Excel apply
export async function applyImport(
  sectionId: number,
  file: File
): Promise<ImportApplyResponse> {
  const formData = new FormData();
  formData.append('file', file);

  const { data } = await apiClient.post(
    `/instructor/section/${sectionId}/grades/import-apply`,
    formData,
    {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    }
  );

  return data;
}