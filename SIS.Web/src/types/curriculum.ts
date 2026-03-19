export interface CurriculumStatusDto {
  studentName: string;
  programName: string;
  curriculumVersion: string;
  totalCredits: number;
  totalEcts: number;
  cgpa: number;
  passedCredits: number;
  passedEcts: number;
  semesters: CurriculumSemesterDto[];
}

export interface CurriculumSemesterDto {
  semesterNumber: number;
  label: string;
  courses: CurriculumCourseStatusDto[];
  totalCredits: number;
  totalEcts: number;
  courseCount: number;
  compulsoryCount: number;
  electiveCount: number;
}

export interface CurriculumCourseStatusDto {
  courseId: number;
  code: string;
  name: string;
  isCompulsory: boolean;
  credits: number;
  ects: number;
  takenTermLabel: string | null;
  takenCourseCode: string | null;
  takenCourseName: string | null;
  takenIsCompulsory: boolean | null;
  takenCredits: number | null;
  takenEcts: number | null;
  grade: string | null;
  gradeStatus: string | null;
  prerequisites: string[];
  prerequisitesMet: boolean;
}
