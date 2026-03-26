import apiClient from './axios';

export interface TermClosingStepStatus {
  step: number;
  label: string;
  isCompleted: boolean;
  completedAt: string | null;
  completedByName: string | null;
  notes: string | null;
}

export interface TermClosingStatusDto {
  departmentId: number;
  termId: number;
  termName: string;
  steps: TermClosingStepStatus[];
}

export interface TermClosingStepResultDto {
  success: boolean;
  message: string;
}

export interface GpaRecalculationResultDto {
  totalStudents: number;
  recalculatedCount: number;
  changedCount: number;
  notes: string[];
}

export interface StudentProgressionDto {
  studentId: number;
  studentNumber: string;
  fullName: string;
  oldLevel: number;
  oldLevelLabel: string;
  newLevel: number;
  newLevelLabel: string;
  completedEcts: number;
  cgpa: number;
  sgpa: number;
  wasPromoted: boolean;
  status: string;
  warnings: string[];
}

export interface BatchProgressionResultDto {
  totalStudentsAnalyzed: number;
  promotedCount: number;
  stayedCount: number;
  warningCount: number;
  results: StudentProgressionDto[];
  notes: string[];
}

export interface NewTermRequestDto {
  name: string;
  year: number;
  semester: number;
  registrationStart: string;
  registrationEnd: string;
  termStart: string;
  termEnd: string;
}

export interface NewTermResultDto {
  success: boolean;
  message: string;
  newTermId: number | null;
}

export async function getTermClosingStatus(departmentId: number): Promise<TermClosingStatusDto> {
  const { data } = await apiClient.get<TermClosingStatusDto>(`/term-closing/${departmentId}/status`);
  return data;
}

export async function lockGrades(departmentId: number): Promise<TermClosingStepResultDto> {
  const { data } = await apiClient.post<TermClosingStepResultDto>(`/term-closing/${departmentId}/lock-grades`);
  return data;
}

export async function unlockGrades(departmentId: number): Promise<TermClosingStepResultDto> {
  const { data } = await apiClient.post<TermClosingStepResultDto>(`/term-closing/${departmentId}/unlock-grades`);
  return data;
}

export async function calculateGpa(departmentId: number): Promise<GpaRecalculationResultDto> {
  const { data } = await apiClient.post<GpaRecalculationResultDto>(`/term-closing/${departmentId}/calculate-gpa`);
  return data;
}

export async function previewPromotion(departmentId: number): Promise<BatchProgressionResultDto> {
  const { data } = await apiClient.get<BatchProgressionResultDto>(`/term-closing/${departmentId}/preview-promote`);
  return data;
}

export async function promote(departmentId: number): Promise<BatchProgressionResultDto> {
  const { data } = await apiClient.post<BatchProgressionResultDto>(`/term-closing/${departmentId}/promote`);
  return data;
}

export async function rollbackPromotion(departmentId: number): Promise<TermClosingStepResultDto> {
  const { data } = await apiClient.post<TermClosingStepResultDto>(`/term-closing/${departmentId}/rollback-promote`);
  return data;
}

export async function openNewTerm(departmentId: number, request: NewTermRequestDto): Promise<NewTermResultDto> {
  const { data } = await apiClient.post<NewTermResultDto>(`/term-closing/${departmentId}/open-new-term`, request);
  return data;
}
