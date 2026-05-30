// SIS.Web/src/api/grade.api.ts
import apiClient from './axios';
import type {
  ButunlemeFinalizeResponse,
  ButunlemeRosterResponse,
  ButunlemeScoreItem,
  ExamAttendance,
  ExamOverviewRow,
  FinalizeResponse,
  GradeComponent,
  GradeComponentType,
  GradeSubmitDto,
  GradesResponse,
  GradingCriteria,
  ImportApplyResponse,
  ImportPreviewResponse,
  MazeretFinalizeResponse,
  MazeretRosterResponse,
  MazeretScoreItem,
  PasteScoresRequest,
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

// Komponent ilan (hoca öğrenciye bu bileşenin notunu yayınlar / kaldırır).
// Tek-kaynak görünürlük kapısı: WebModuleControl (admin) AND PublishedAt (hoca).
export async function publishComponent(componentId: number): Promise<{ publishedAt: string }> {
  const { data } = await apiClient.post(`/instructor/components/${componentId}/publish`);
  return data;
}

export async function unpublishComponent(componentId: number): Promise<void> {
  await apiClient.post(`/instructor/components/${componentId}/unpublish`);
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

// Sınav katılım işaretleme
export async function setExamAttendance(
  sectionId: number,
  componentId: number,
  items: { studentId: number; attendance: ExamAttendance }[]
): Promise<void> {
  await apiClient.put(`/instructor/section/${sectionId}/exam-attendance`, {
    componentId,
    items,
  });
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

// ── Paste import ───────────────────────────────────────

export async function pastePreview(
  sectionId: number,
  req: PasteScoresRequest
): Promise<ImportPreviewResponse> {
  const { data } = await apiClient.post(
    `/instructor/section/${sectionId}/grades/paste-preview`,
    req
  );
  return data;
}

export async function pasteApply(
  sectionId: number,
  req: PasteScoresRequest
): Promise<ImportApplyResponse> {
  const { data } = await apiClient.post(
    `/instructor/section/${sectionId}/grades/paste-apply`,
    req
  );
  return data;
}

// ── Exams overview ──────────────────────────────────────

export async function getExamsOverview(termId?: number): Promise<ExamOverviewRow[]> {
  const { data } = await apiClient.get('/instructor/exams-overview', {
    params: termId != null ? { termId } : undefined,
  });
  return data;
}

// ── Bütünleme ───────────────────────────────────────────

export async function getButunlemeRoster(sectionId: number): Promise<ButunlemeRosterResponse> {
  const { data } = await apiClient.get(`/instructor/section/${sectionId}/makeup/butunleme`);
  return data;
}

export async function setButunlemeScores(
  sectionId: number,
  items: ButunlemeScoreItem[]
): Promise<void> {
  await apiClient.put(`/instructor/section/${sectionId}/makeup/butunleme-scores`, { items });
}

export async function finalizeButunleme(sectionId: number): Promise<ButunlemeFinalizeResponse> {
  const { data } = await apiClient.post(
    `/instructor/section/${sectionId}/makeup/butunleme-finalize`
  );
  return data;
}

// ── Mazeret ─────────────────────────────────────────────

export async function getMazeretRoster(sectionId: number): Promise<MazeretRosterResponse> {
  const { data } = await apiClient.get(`/instructor/section/${sectionId}/makeup/mazeret`);
  return data;
}

export async function setMazeretScores(
  sectionId: number,
  items: MazeretScoreItem[]
): Promise<void> {
  await apiClient.put(`/instructor/section/${sectionId}/makeup/mazeret-scores`, { items });
}

export async function finalizeMazeret(sectionId: number): Promise<MazeretFinalizeResponse> {
  const { data } = await apiClient.post(
    `/instructor/section/${sectionId}/makeup/mazeret-finalize`
  );
  return data;
}