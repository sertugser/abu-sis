// SIS.Web/src/types/grade.ts

export type GradeComponentType = 'Other' | 'Midterm' | 'Final';

export interface GradeComponent {
  id: number;
  name: string;
  weight: number;
  order: number;
  type: GradeComponentType;
  /** Hocanın bu komponenti "ilan ettiği" an (UTC ISO). null = ilan edilmedi. */
  publishedAt?: string | null;
}

export interface StudentScore {
  studentId: number;
  score: number | null;
}

export interface GradeSubmitDto {
  componentId: number;
  grades: StudentScore[];
}

export type ExamAttendance = 'NotMarked' | 'Present' | 'Absent' | 'Excused';

export interface StudentGradeRow {
  studentId: number;
  studentNumber: string;
  firstName: string;
  lastName: string;
  letterGrade?: string | null;
  weightedAverage?: number | null;
  scores: Record<number, number | null>;
  examAttendances?: Record<number, ExamAttendance>;
}

export interface GradesResponse {
  sectionId: number;
  courseCode: string;
  courseName: string;
  isFinalized: boolean;
  hideStatistics?: boolean;
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
  editable?: boolean;
}

export interface FinalizePreviewItem {
  studentId: number;
  weightedAverage: number;
  letterGrade: string;
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
  currentScore: number | null;
  newScore: number | null;
  rawNewScore: string;
  status: 'Valid' | 'Update' | 'Warning' | 'Error' | 'Empty';
  message: string;
}

export interface ImportPreviewResponse {
  sectionId: number;
  componentId: number;
  componentName: string;
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
  componentId: number;
  inserted: number;
  updated: number;
  skipped: number;
}

// ══════════════════════════════════════════════════════════
// BÜTÜNLEME (RESIT MAKEUP) TYPES
// ══════════════════════════════════════════════════════════

export interface ButunlemeVizeScore {
  componentId: number;
  componentName: string;
  score: number | null;
}

export interface ButunlemeFinalScore {
  componentId: number;
  componentName: string;
  score: number | null;
  makeupScore: number | null;
}

export interface ButunlemeStudent {
  studentId: number;
  studentNumber: string;
  firstName: string;
  lastName: string;
  currentGrade: string | null;
  vizeScores: ButunlemeVizeScore[];
  finalScores: ButunlemeFinalScore[];
}

export interface ButunlemeRosterResponse {
  sectionId: number;
  finalComponentId: number | null;
  students: ButunlemeStudent[];
}

export interface ButunlemeScoreItem {
  studentId: number;
  makeupScore: number;
}

export interface ButunlemeFinalizeResult {
  studentId: number;
  oldGrade: string | null;
  newGrade: string;
  weightedAverage: number;
}

export interface ButunlemeFinalizeResponse {
  message: string;
  results: ButunlemeFinalizeResult[];
}

// ══════════════════════════════════════════════════════════
// MAZERET (EXCUSE MAKEUP) TYPES
// ══════════════════════════════════════════════════════════

export interface ExcusedComponent {
  componentId: number;
  componentName: string;
  originalScore: number | null;
  makeupScore: number | null;
}

export interface MazeretStudent {
  studentId: number;
  studentNumber: string;
  firstName: string;
  lastName: string;
  currentGrade: string | null;
  excusedComponents: ExcusedComponent[];
}

export interface MazeretRosterResponse {
  sectionId: number;
  students: MazeretStudent[];
}

export interface MazeretScoreItem {
  studentId: number;
  componentId: number;
  makeupScore: number;
}

export interface MazeretFinalizeResult {
  studentId: number;
  oldGrade: string | null;
  newGrade: string;
  weightedAverage: number;
}

export interface MazeretFinalizeResponse {
  message: string;
  results: MazeretFinalizeResult[];
}

// ══════════════════════════════════════════════════════════
// PASTE IMPORT TYPES
// ══════════════════════════════════════════════════════════

export interface PasteScoresRequest {
  componentId: number;
  text: string;
  separator: string;
}

// ══════════════════════════════════════════════════════════
// EXAMS OVERVIEW TYPES
// ══════════════════════════════════════════════════════════

export interface ExamOverviewComponent {
  id: number;
  name: string;
  type: GradeComponentType;
  weight: number;
  publishedAt: string | null;
  examDate: string | null;
}

export interface ExamOverviewRow {
  sectionId: number;
  courseCode: string;
  courseName: string;
  sectionNumber: number;
  isFinalized: boolean;
  components: ExamOverviewComponent[];
}
