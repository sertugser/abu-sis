export interface StudentCourseEligibilityDto {
  studentId: number;
  studentNumber: string;
  fullName: string;
  courseId: number;
  courseCode: string;
  courseName: string;
  ects: number;
  isCompulsory: boolean;
  suggestedSemester: number;
  isEligible: boolean;
  takeType: string;
  previousGrade?: string | null;
  currentSemester: number;
  cgpa: number;
  currentTermEcts: number;
  ectsLimit: number;
  reasons: IneligibilityReasonDto[];
}

export interface IneligibilityReasonDto {
  reason: string;
  message: string;
}
