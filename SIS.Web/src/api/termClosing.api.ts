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

// ── New DTOs for enhanced features ──

export interface SectionGradeDetailDto {
  sectionId: number;
  courseCode: string;
  courseName: string;
  sectionCode: string;
  instructorName: string;
  totalRegistrations: number;
  enteredGradeCount: number;
  missingGradeCount: number;
  isFinalized: boolean;
  entryPercentage: number;
}

export interface GradeLockDetailResultDto {
  totalSections: number;
  totalActiveRegistrations: number;
  missingGradeCount: number;
  allGradesEntered: boolean;
  hasActiveRegistrations: boolean;
  finalizedSectionCount: number;
  overallEntryPercentage: number;
  sections: SectionGradeDetailDto[];
  notes: string[];
}

export interface GradeDistributionItemDto {
  gradeCode: string;
  count: number;
  percentage: number;
}

export interface GradeDistributionResultDto {
  totalGraded: number;
  averageScore: number;
  passRate: number;
  grades: GradeDistributionItemDto[];
}

export interface DepartmentClosingOverviewDto {
  departmentId: number;
  departmentCode: string;
  departmentName: string;
  completedStepCount: number;
  currentStepLabel: string;
  totalSections: number;
  missingGradeCount: number;
  lastActivityAt: string | null;
  lastActivityByName: string | null;
}

// ── Existing API functions ──

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

// ── New API functions ──

export async function getGradeReadinessDetail(departmentId: number): Promise<GradeLockDetailResultDto> {
  const { data } = await apiClient.get<GradeLockDetailResultDto>(`/term-closing/${departmentId}/grade-readiness-detail`);
  return data;
}

export async function getGradeDistribution(departmentId: number): Promise<GradeDistributionResultDto> {
  const { data } = await apiClient.get<GradeDistributionResultDto>(`/term-closing/${departmentId}/grade-distribution`);
  return data;
}

export async function getDepartmentClosingOverview(facultyId?: number): Promise<DepartmentClosingOverviewDto[]> {
  const params = facultyId ? { facultyId } : {};
  const { data } = await apiClient.get<DepartmentClosingOverviewDto[]>('/term-closing/overview', { params });
  return data;
}

export async function exportPromotionReport(departmentId: number): Promise<Blob> {
  const { data } = await apiClient.get(`/term-closing/${departmentId}/export-promotion`, { responseType: 'blob' });
  return data;
}
