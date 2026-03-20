// SIS.Web/src/types/grade.ts

export interface GradeComponent {
  id: number;
  name: string;
  weight: number;
  order: number;
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
  scores: Record<number, number | null>; // componentId -> score
}

export interface GradesResponse {
  sectionId: number;
  courseCode: string;
  courseName: string;
  isFinalized: boolean;
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
