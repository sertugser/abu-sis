export type AdvisorConstraintType = 'Block' | 'Warning';

export interface AdvisorCourseConstraintDto {
  id: number;
  studentId: number;
  studentNumber: string;
  studentName: string;
  courseId: number;
  courseCode: string;
  courseName: string;
  termId: number;
  termName: string;
  advisorId: number;
  constraintType: AdvisorConstraintType;
  note?: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface UpsertAdvisorCourseConstraintRequest {
  studentId: number;
  courseId: number;
  termId?: number | null;
  constraintType: AdvisorConstraintType;
  note?: string | null;
}

export interface UpdateAdvisorCourseConstraintRequest {
  constraintType: AdvisorConstraintType;
  note?: string | null;
  isActive: boolean;
}

export interface AdvisorStudentListItemDto {
  studentId: number;
  studentNumber: string;
  studentName: string;
  programName: string;
  completedSemesters: number;
  cgpa: number;
}
