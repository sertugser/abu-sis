// SIS.Web/src/types/grade.ts

export interface GradeComponent {
  id: number;
  name: string;
  weight: number;
  order: number;
  minimumScore?: number | null;
}

export interface StudentScore {
  studentId: number;
  score: number | null;
}

export interface GradeSubmitDto {
  componentId: number;
  grades: StudentScore[];
}

export interface StudentGradeRow {
  studentId: number;
  studentNumber: string;
  firstName: string;
  lastName: string;
  letterGrade?: string | null;
  weightedAverage?: number | null;
  barrierFailureReason?: string | null;
  scores: Record<number, number | null>;
}

export interface GradesResponse {
  sectionId: number;
  courseCode: string;
  courseName: string;
  isFinalized: boolean;
  areGradesPublished: boolean;
  gradesPublishedAt?: string | null;
  calculationMethod?: 'Absolute' | 'Relative' | string;
  components: GradeComponent[];
  students: StudentGradeRow[];
}

export interface GradingCriteria {
  aaMin: number;
  baMin: number;
  bbMin: number;
  cbMin: number;
  ccMin: number;
  dcMin: number;
  ddMin: number;
  calculationMethod?: 'Absolute' | 'Relative';
}

export interface FinalizePreviewItem {
  studentId: number;
  weightedAverage: number;
  letterGrade: string;
  barrierFailureReason?: string | null;
}

export interface FinalizeResponse {
  message: string;
  results: FinalizePreviewItem[];
}
// ══════════════════════════════════════════════════════════
// EXCEL IMPORT TYPES
// ══════════════════════════════════════════════════════════

export interface ImportPreviewRow {
  rowNumber: number;
  studentId: number | null;
  studentNumber: string;
  firstName: string;
  lastName: string;
  componentId?: number | null;
  componentName?: string;
  currentScore: number | null;
  newScore: number | null;
  rawNewScore: string;
  status: 'Valid' | 'Update' | 'Warning' | 'Error' | 'Empty';
  message: string;
}

export interface ImportPreviewResponse {
  sectionId: number;
  componentId?: number | null;
  componentName?: string | null;
  totalRows: number;
  validCount: number;
  warningCount: number;
  errorCount: number;
  emptyCount: number;
  rows: ImportPreviewRow[];
}

export interface ImportApplyResponse {
  message: string;
  sectionId: number;
  componentId?: number | null;
  inserted: number;
  updated: number;
  skipped: number;
}
